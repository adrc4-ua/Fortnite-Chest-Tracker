using System.Diagnostics;
using System.Runtime.Versioning;

namespace FCT.Monitoring;

/// <summary>
/// FCT while it runs: one background thread owns the history (a scan can take minutes), reads new replays, follows the
/// match in progress, the treasure screen, updates and the automatic close, and publishes a <see cref="MonitorState"/>
/// for the window. The window only asks for changes, so it never freezes while replays are read.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AppMonitor
{
    private const int NoticeSeconds = 20;

    private readonly Store _store;
    private readonly TrackerData _data;
    private readonly Tracker _tracker;
    private readonly SettingsGate _settings;
    private readonly EarlierChests _earlier;
    private readonly PlayerFollower _follower;
    private readonly TreasureScreenCapture _screens;
    private readonly UpdateFlow _updates;
    private readonly AutoResetEvent _wake = new(false);

    private FileSystemWatcher? _watcher;
    private GameLogWatcher? _gameLog;
    private volatile bool _stop, _publishRequested, _statsRequested, _scanned;
    private volatile string? _shownKey, _shownName, _notice, _resetBackup;
    private volatile ScanProgress? _progress;
    private DateTime _noticeUntil, _nextScan = DateTime.UtcNow, _nextGameCheck, _nextDisplayCheck;
    private DateTime? _fortniteClosedSince;
    private bool _closeRequested, _displayTipShown;
    private volatile string? _displayWarning;

    public AppMonitor(Store store, Config config, TrackerData data, Tracker tracker, string dataDir)
    {
        _store = store;
        _data = data;
        _tracker = tracker;
        DataFolder = dataDir;
        _settings = new SettingsGate(store, config);
        _earlier = new EarlierChests(_settings, data);
        _follower = new PlayerFollower(data, tracker, () => config);
        _screens = new TreasureScreenCapture(() => _wake.Set());
        _updates = new UpdateFlow(() => config, RequestPublish);
        // A batch of replays (first run, or many at once): the window shows a loading notice, like the stats page.
        _tracker.Progressed = progress =>
        {
            _progress = progress;
            Publish();
        };
    }

    // ---- What the window reads ----------------------------------------------------------------------------------------

    /// <summary>Current settings (read only: changes go through <see cref="Change"/>).</summary>
    public Config Config => _settings.Config;

    public string DataFolder { get; }

    /// <summary>Opened by "Open with Fortnite" (starts in the background, with the overlay).</summary>
    public bool AutoMode { get; set; }

    /// <summary>The window is hidden in the notification area: closing Fortnite closes FCT once the last match is counted.</summary>
    public volatile bool InBackground;

    /// <summary>New state, from the monitor thread (the window must marshal it).</summary>
    public event Action<MonitorState>? Changed;

    /// <summary>Asks the window to close (Fortnite closed, new version, restart, uninstall), from any thread.</summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Fortnite is in exclusive fullscreen, where the overlay cannot be seen: raised once per run, from the monitor
    /// thread. The window tells the player (a notification now, a message once they leave the game).
    /// </summary>
    public event Action? ExclusiveFullscreen;

    public bool RestartAfterUpdate => _updates.Installed;
    public bool RestartRequested { get; private set; }
    public bool UninstallRequested { get; private set; }

    public string? ShownProfileName => _shownName;

    /// <summary>Epic id of the profile on screen (its key, when the replays carry one), or null.</summary>
    public string? ShownAccountId => _shownKey is { Length: 32 } key && key.All(Uri.IsHexDigit) ? key : null;

    /// <summary>Where to open the stats page: served by FCT (its settings work there), or else the file.</summary>
    public string StatsTarget => ObsServer.StatsUrl ?? _store.StatsPath;

    public bool OverlayEnabled => OperatingSystem.IsWindows();
    public bool OverlayVisible => OperatingSystem.IsWindows() && NativeOverlay.IsVisible;

    /// <summary>
    /// Days with matches of the profile on screen (all of them, ignoring the "count from" date), newest first, with how
    /// many matches each: the choices of "count from".
    /// </summary>
    public IReadOnlyList<(DateTime Day, int Matches)> MatchDays()
    {
        try
        {
            var key = Profiles.Current(_data, Config)?.Key;
            return _data.Matches.ToList()
                .Where(m => (key == null || Profiles.Key(m) == key) && Profiles.IsEventMatch(m, Config))
                .GroupBy(Profiles.Day)
                .OrderByDescending(day => day.Key)
                .Select(day => (day.Key, day.Count()))
                .ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<(DateTime, int)>(); // the list changed while being read: no days this time
        }
    }

    // ---- Life cycle ----------------------------------------------------------------------------------------------------

    public void Start()
    {
        // Saved treasure screens again with today's rules: fixes what an older version assigned wrongly.
        try
        {
            var fixedAny = false;
            foreach (var match in _data.Matches)
            {
                fixedAny |= TreasureScreenMerge.Apply(match, _data.TreasureScreens);
            }
            if (fixedAny)
            {
                _store.SaveData(_data);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Repasar pantallas de tesoros", ex);
        }

        StartOverlay();
        WatchReplays();
        ObsServer.StatsFile = _store.StatsPath;
        ObsServer.StatsLang = L.Code;
        ObsServer.Settings = ApplyFromStatsPage;
        ObsServer.Start(Config.ObsPort > 0 ? Config.ObsPort : ObsServer.DefaultPort);
        _gameLog = new GameLogWatcher(_screens.Shown);
        new Thread(Run) { IsBackground = true, Name = "Vigilancia" }.Start();
    }

    public void Stop()
    {
        ObsServer.Stop();
        _stop = true;
        _wake.Set();
        _watcher?.Dispose();
        _gameLog?.Dispose();
    }

    /// <summary>Closes FCT to start it again (texts are read at start: a language change needs it).</summary>
    public void Restart()
    {
        RestartRequested = true;
        CloseRequested?.Invoke();
    }

    /// <summary>Closes FCT; on the way out, its task, startup entries and exe are removed (the data stays).</summary>
    public void Uninstall()
    {
        UninstallRequested = true;
        CloseRequested?.Invoke();
    }

    /// <summary>The player keeps FCT open after Fortnite closes.</summary>
    public void KeepOpen()
    {
        AutoMode = false;
        RequestPublish();
    }

    public void RequestPublish()
    {
        _publishRequested = true;
        _wake.Set();
    }

    /// <summary>A message where the window's buttons are; it goes away by itself after a while.</summary>
    public void SetNotice(string? text) => SetNotice(text, NoticeSeconds);

    /// <summary>A message where the window's buttons are, for <paramref name="seconds"/>.</summary>
    public void SetNotice(string? text, int seconds)
    {
        _notice = text;
        _noticeUntil = DateTime.UtcNow.AddSeconds(seconds);
        RequestPublish();
    }

    // ---- Settings ------------------------------------------------------------------------------------------------------

    public void Change(Action<Config> change)
    {
        _settings.Edit(change);
        RequestPublish();
    }

    /// <summary>A change that the stats page shows too: the page is rewritten right after.</summary>
    private void ChangeShown(Action<Config> change)
    {
        _statsRequested = true;
        Change(change);
    }

    public void SetProfile(string key)
    {
        Change(c => c.Profile = key);
        _statsRequested = true;
        _wake.Set();
    }

    public void SetCountExtra(Action<Config> change) => ChangeShown(change);
    public void SetCountTeammates(bool count) => ChangeShown(c => c.CountTeammateChests = count);
    public void SetCountFrom(DateTime? from) => ChangeShown(c => c.CountFrom = from?.Date);
    public void SetShowTeammates(bool show) => ChangeShown(c => c.ShowTeammates = show);
    public void SetCountRanked(bool count) => ChangeShown(c => c.CountRanked = count);
    public void SetLanguage(string? code) => ChangeShown(c => c.Language = code);

    public void ChangeDemosFolder(string folder)
    {
        Change(c => c.DemosFolder = folder);
        WatchReplays();
    }

    /// <summary>"Open with Fortnite". False when it cannot be set up (FCT not run from its exe).</summary>
    public bool SetStartWithFortnite(bool on)
    {
        // Another data folder (--data, tests) only keeps the choice: Windows' startup belongs to the normal one.
        var normalFolder = string.Equals(Path.GetFullPath(DataFolder).TrimEnd('\\', '/'), Path.GetFullPath(Store.DefaultFolder()).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        var done = true;
        _settings.Edit(c =>
        {
            if (normalFolder && (!OperatingSystem.IsWindows() || !FortniteWatch.Apply(on, DataFolder)))
            {
                done = false;
                return;
            }
            c.StartWithFortnite = on;
        });
        if (done)
        {
            RequestPublish();
        }
        return done;
    }

    /// <summary>
    /// A setting changed on the stats page (from the local server's thread). Every one is the app's own: the window and its
    /// settings show it at once, and a new language restarts the app.
    /// </summary>
    private bool ApplyFromStatsPage(string key, string value)
    {
        if (key == "lang")
        {
            if (value.Length > 0 && !L.Languages.Any(l => l.Code == value))
            {
                return false;
            }
            var code = value.Length == 0 ? null : value;
            var differs = Config.Language != code;
            SetLanguage(code);
            if (differs && AppInfo.ExePath != null)
            {
                _ = Task.Delay(250).ContinueWith(_ => Restart()); // lets the server answer and the page be rewritten first
            }
            return true;
        }
        if (key == "mark")
        {
            return MarkPrize(value);
        }
        var on = value == "1";
        Action<Config>? change = key switch
        {
            "teammates" => c => c.ShowTeammates = on,
            "countMates" => c => c.CountTeammateChests = on,
            "ammo" => c => c.CountAmmoBoxes = on,
            "food" => c => c.CountFoodBoxes = on,
            "cannons" => c => c.CountLootCannons = on,
            "ranked" => c => c.CountRanked = on,
            _ => null,
        };
        if (change == null)
        {
            return false;
        }
        ChangeShown(change);
        return true;
    }

    /// <summary>A prize of the weekly lists struck off or brought back on the stats page: "1|name" or "0|name".</summary>
    private bool MarkPrize(string value)
    {
        var parts = value.Split('|', 2);
        if (parts.Length != 2 || parts[0] is not ("1" or "0") || !Prizes.All.Any(p => p.Week > 0 && p.Name == parts[1]))
        {
            return false;
        }
        var (struck, name) = (parts[0] == "1", parts[1]);
        ChangeShown(c => c.PrizeMarks[name] = struck); // rewritten so the next reload keeps it
        return true;
    }

    /// <summary>
    /// Empties the history (and the chests before FCT); replays already in the folder are not counted again. data.json is
    /// copied first; the path of that copy is returned. Done by the monitor thread, which owns the history.
    /// </summary>
    public string ResetStats()
    {
        var copy = Path.Combine(_store.Folder, $"data-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        _resetBackup = copy;
        _wake.Set();
        return copy;
    }

    // ---- Chests before FCT ------------------------------------------------------------------------------------------------

    public int InitialChests => _earlier.Chests;
    public HistoryEstimate? Estimate => _earlier.Estimate;

    public HistoryEstimator.Result PreviewEstimate(int totalMatches, int sample, double? manualAverage) => _earlier.Preview(totalMatches, sample, manualAverage);

    public void SetInitialChests(int count)
    {
        _earlier.SetChests(count);
        RequestPublish();
        _statsRequested = true;
        _wake.Set();
    }

    public void SetEstimate(int? totalMatches, string source, int sample, double? manualAverage)
    {
        _earlier.SetEstimate(totalMatches, source, sample, manualAverage);
        RequestPublish();
        _statsRequested = true;
        _wake.Set();
    }

    // ---- Overlay and updates ----------------------------------------------------------------------------------------------

    public void ToggleOverlay()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        if (NativeOverlay.IsRunning)
        {
            NativeOverlay.Toggle();
            return;
        }
        _settings.Edit(c => c.OverlayVisible = true);
        StartOverlay();
        RequestPublish();
    }

    public void InstallUpdate() => _updates.Install(
        notice: text => _notice = text,
        failed: SetNotice,
        restart: () => CloseRequested?.Invoke());

    private void StartOverlay()
    {
        var c = Config;
        NativeOverlay.Start(c.OverlayX, c.OverlayY, c.OverlayLocked,
            (x, y) => _settings.Edit(s => (s.OverlayX, s.OverlayY) = (x, y)),
            locked => _settings.Edit(s => s.OverlayLocked = locked),
            c.OverlayVisible,
            visible =>
            {
                _settings.Edit(s => s.OverlayVisible = visible);
                RequestPublish(); // the window's overlay button changes colour
            },
            onClose: () => CloseRequested?.Invoke(), // the overlay's ✕ closes FCT
            isGame: FortniteWatch.IsGameProcess);    // only on screen over the game
    }

    // ---- The monitor thread -------------------------------------------------------------------------------------------------

    private void Run()
    {
        WriteStatsPageAtStart();
        var changed = true;
        while (!_stop)
        {
            changed |= _publishRequested;
            _publishRequested = false;
            changed |= FileTreasureScreens();
            changed |= ResetIfAsked();
            try
            {
                changed |= ScanIfDue();
                changed |= CloseIfFortniteGone();
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Vigilancia", ex);
            }
            changed |= FollowPlayer();
            changed |= ApplyEstimates();
            _updates.Poll();
            changed |= _updates.Handle(idle: !_tracker.IsLive && !_tracker.HasPending && !_tracker.IsFinishing, InstallUpdate);
            changed |= ExpireNotice();
            changed |= WarnAboutExclusiveFullscreen();
            if (_statsRequested)
            {
                _statsRequested = false;
                StatsPage.WriteAll(_store, Config, _data);
            }
            if (changed || _publishRequested)
            {
                _publishRequested = false;
                Publish();
            }
            changed = false;
            _wake.WaitOne(TimeSpan.FromSeconds(1)); // until something happens, or a second
        }
    }

    /// <summary>
    /// Before reading replays: the stats page is rewritten (after an update it would show the old version's until the
    /// next match) and opened if "open stats at startup" is on — not when started by Fortnite, to keep the game in front.
    /// </summary>
    private void WriteStatsPageAtStart()
    {
        StatsPage.WriteAll(_store, Config, _data);
        if (!Config.OpenStatsOnStart || AutoMode || !File.Exists(_store.StatsPath))
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(StatsTarget) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // no browser: the window's "Stats" button still works
        }
    }

    /// <summary>Reads what is ready; then again in 2 s (a match just ended), 15 s (one is settling) or as a fallback.</summary>
    private bool ScanIfDue()
    {
        if (DateTime.UtcNow < _nextScan)
        {
            return false;
        }
        var added = _tracker.Scan();
        _scanned = true;
        var liveChanged = _tracker.UpdateLive();
        var fallback = _watcher != null && !AutoMode ? 60 : 10;
        _nextScan = DateTime.UtcNow.AddSeconds(_tracker.IsFinishing ? 2 : _tracker.HasPending ? 15 : fallback);
        return added > 0 || liveChanged;
    }

    private bool FileTreasureScreens()
    {
        if (!_screens.FileInto(_data))
        {
            return false;
        }
        try
        {
            _store.SaveData(_data);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Guardar pantalla de tesoros", ex);
        }
        _statsRequested = true;
        return true;
    }

    private bool ResetIfAsked()
    {
        if (_resetBackup is not { } copy)
        {
            return false;
        }
        _resetBackup = null;
        try
        {
            _tracker.Reset(copy);
            _settings.Edit(c =>
            {
                c.InitialChests.Clear();
                c.Profile = null;
            });
            _statsRequested = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Restablecer estadísticas", ex);
            SetNotice(L.F("No se pudo restablecer: {0}", "Could not reset: {0}", ex.Message));
            return false;
        }
    }

    private bool FollowPlayer()
    {
        if (_follower.NewPlayer() is not { } player)
        {
            return false;
        }
        Change(c => c.Profile = player.Key);
        _statsRequested = true; // the stats page follows too
        SetNotice(L.F("Ahora juega {0}: se muestra su perfil.", "{0} is playing now: showing their profile.", player.Name));
        return true;
    }

    /// <summary>Pending estimates are computed once the replays are read and no match is being played or read.</summary>
    private bool ApplyEstimates()
    {
        if (!_scanned || _tracker.HasPending || _tracker.IsLive || _tracker.IsFinishing)
        {
            return false;
        }
        var notices = _earlier.ApplyPending();
        foreach (var notice in notices)
        {
            SetNotice(notice);
        }
        if (notices.Count == 0)
        {
            return false;
        }
        _statsRequested = true;
        return true;
    }

    /// <summary>
    /// With the overlay on and Fortnite open in exclusive fullscreen, a lasting notice in the window and, once per run, a
    /// Windows notification; both go when the mode changes. Checked every 10 s from the game's settings file.
    /// </summary>
    private bool WarnAboutExclusiveFullscreen()
    {
        var now = DateTime.UtcNow;
        if (!OperatingSystem.IsWindows() || now < _nextDisplayCheck)
        {
            return false;
        }
        _nextDisplayCheck = now.AddSeconds(10);
        var exclusive = OverlayVisible && FortniteWatch.IsFortniteRunning() && GameDisplayMode.IsExclusiveFullscreen() == true;
        var warning = exclusive
            ? L.T("En Pantalla completa el overlay no se ve. Pon Fortnite en Pantalla completa en ventana (Ajustes → Vídeo).",
                "In Fullscreen the overlay can't show. Set Fortnite to Windowed Fullscreen (Settings → Video).")
            : null;
        if (warning == _displayWarning)
        {
            return false;
        }
        _displayWarning = warning;
        if (warning != null && !_displayTipShown)
        {
            _displayTipShown = true;
            ExclusiveFullscreen?.Invoke();
        }
        return true;
    }

    private bool ExpireNotice()
    {
        if (_notice == null || DateTime.UtcNow < _noticeUntil)
        {
            return false;
        }
        _notice = null;
        return true;
    }

    /// <summary>
    /// In the background, once Fortnite has closed, FCT closes as soon as the last match is counted (5 minutes at most).
    /// With the window in view, or the game running, it stays.
    /// </summary>
    private bool CloseIfFortniteGone()
    {
        var now = DateTime.UtcNow;
        if (!InBackground || _closeRequested)
        {
            _fortniteClosedSince = null;
            return false;
        }
        if (now < _nextGameCheck)
        {
            return false; // looking at the processes costs something: every 3 s is enough
        }
        _nextGameCheck = now.AddSeconds(3);
        if (FortniteWatch.IsFortniteRunning())
        {
            _fortniteClosedSince = null;
            return false;
        }
        _fortniteClosedSince ??= now;
        var lastMatchCounted = !_tracker.IsLive && !_tracker.HasPending && !_tracker.IsFinishing;
        if (lastMatchCounted || now - _fortniteClosedSince.Value >= TimeSpan.FromMinutes(5))
        {
            _closeRequested = true;
            CloseRequested?.Invoke();
        }
        return false;
    }

    private void Publish()
    {
        var config = Config;
        var shown = Profiles.Current(_data, config);
        _shownKey = shown?.Key;
        _shownName = shown?.Name;
        _earlier.ShownKey = _shownKey;
        var view = Profiles.View(_data, config);
        _earlier.ShowMatches(view.Matches);

        var live = _follower.LiveOfShown();
        var overlay = OverlayModel.Build(view, live, _tracker.IsLive && live != null, Profiles.InitialChests(_data, config));
        StatsPage.WriteOverlay(_store, overlay);
        ObsServer.Update(overlay);
        if (OperatingSystem.IsWindows() && NativeOverlay.IsRunning)
        {
            NativeOverlay.Update(overlay);
        }

        var shownLive = _tracker.IsLive ? live : null;
        var notice = _updates.Installing ? L.T("Descargando la actualización…", "Downloading the update…")
            : _progress is { } progress ? L.F("Leyendo tus replays… {0} / {1}. Las cifras suben a medida que se leen.", "Reading your replays… {0} / {1}. The numbers grow as they are read.", progress.Done, progress.Total)
            : _notice ?? _displayWarning;
        Changed?.Invoke(new MonitorState(overlay, Profiles.List(_data, config), shown?.Key, _updates.Available, notice, false,
            ScreenFigures.Stats(view.Matches, shownLive) with { EarlierMatches = _earlier.Matches(view.Matches) })
        {
            Team = config.ShowTeammates ? ScreenFigures.Team(view.Matches, shownLive, config.ExtraKinds()) : null,
        });
    }

    // ---- The replays folder ---------------------------------------------------------------------------------------------------

    /// <summary>Windows reports changes in the replays folder, so it need not be re-read all the time.</summary>
    private void WatchReplays()
    {
        _watcher?.Dispose();
        _watcher = null;
        try
        {
            if (!Directory.Exists(Config.DemosFolder))
            {
                return;
            }
            _watcher = new FileSystemWatcher(Config.DemosFolder, "*.replay")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Created += (_, _) => ScanSoon();
            _watcher.Changed += (_, _) => ScanSoon();
            _watcher.Renamed += (_, _) => ScanSoon();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _watcher = null; // the periodic fallback scan covers it
        }
    }

    private void ScanSoon()
    {
        var soon = DateTime.UtcNow.AddSeconds(2);
        if (soon < _nextScan)
        {
            _nextScan = soon;
        }
        _wake.Set();
    }
}
