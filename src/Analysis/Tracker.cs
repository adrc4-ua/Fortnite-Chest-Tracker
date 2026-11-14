namespace FCT.Analysis;

/// <summary>
/// Keeps the history in step with the replays folder: every finished replay is read once, its match added (or
/// replaced, when read again by a newer analyzer) and saved. Also follows the match being played.
/// </summary>
public sealed class Tracker
{
    private static readonly TimeSpan ReadLimit = TimeSpan.FromMinutes(3);

    /// <summary>Values of the processed-files table that are final whatever the analyzer version.</summary>
    private const string Skipped = "skipped", OtherMode = "other mode", OlderVersion = "older version";

    private readonly Store _store;
    private readonly Config _config;
    private readonly ReplayFolder _folder;
    private readonly LiveMatch _live;
    private readonly ScanConsole _console;

    public Tracker(Store store, Config config, TrackerData data)
    {
        _store = store;
        _config = config;
        Data = data;
        _folder = new ReplayFolder(() => config.DemosFolder, data.ProcessedFiles);
        _live = new LiveMatch(config);
        _console = new ScanConsole(store, config, data);
    }

    public TrackerData Data { get; }

    /// <summary>A replay is waiting to finish or to settle.</summary>
    public bool HasPending { get; private set; }

    /// <summary>A match has just ended and its replay is about to be read: worth looking again within seconds.</summary>
    public bool IsFinishing { get; private set; }

    /// <summary>A batch revealed a second account: the player may want to pick their profile.</summary>
    public bool NewProfilesFound { get; set; }

    /// <summary>Progress of a batch (null when it ends), for the window's loading notice.</summary>
    public Action<ScanProgress?>? Progressed { get; set; }

    // ---- The folder -------------------------------------------------------------------------------------------------------

    public static string FileKey(FileInfo file) => ReplayFolder.Key(file);
    public IEnumerable<FileInfo> ReplayFiles() => _folder.All();
    public IEnumerable<FileInfo> UnprocessedFiles() => _folder.NotHandled();

    /// <summary>Replays the next scan will read.</summary>
    public int BacklogCount() => _folder.NotHandled().Count(ReplayFolder.NotRecording);

    /// <summary>Records these replays as not to be counted.</summary>
    public void MarkSkipped(IEnumerable<FileInfo> files)
    {
        foreach (var file in files)
        {
            Data.ProcessedFiles[FileKey(file)] = Skipped;
        }
        _store.SaveData(Data);
    }

    /// <summary>
    /// Saved replays left out on purpose ("only new ones" at the first start, or a reset), still in the folder. Read from
    /// another thread too: a copy of the verdicts is taken, again if the scan changes them meanwhile.
    /// </summary>
    public int SkippedOnDisk()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var skipped = Data.ProcessedFiles.Where(e => e.Value == Skipped).Select(e => e.Key).ToHashSet();
                return skipped.Count == 0 ? 0 : _folder.All().Where(ReplayFolder.NotRecording).Count(f => skipped.Contains(FileKey(f)));
            }
            catch (InvalidOperationException) when (attempt < 3)
            {
                Thread.Sleep(20);
            }
        }
    }

    /// <summary>Counts the replays left out after all: the next scan reads them (call on the scanning thread).</summary>
    public void CountSkipped()
    {
        foreach (var file in Data.ProcessedFiles.Where(e => e.Value == Skipped).Select(e => e.Key).ToList())
        {
            Data.ProcessedFiles.Remove(file);
        }
        _store.SaveData(Data);
    }

    /// <summary>
    /// Empties the history (data.json is copied to <paramref name="backupPath"/> first). Replays already in the folder
    /// are marked as skipped, so counting starts with the next match; one being played still counts.
    /// </summary>
    public void Reset(string backupPath)
    {
        if (File.Exists(_store.DataPath))
        {
            File.Copy(_store.DataPath, backupPath, true);
        }
        Data.Matches.Clear();
        Data.ProcessedFiles.Clear();
        MarkSkipped(_folder.All().Where(ReplayFolder.NotRecording).ToList());
    }

    /// <summary>
    /// Brings a backup back: its matches (with their chests and treasures) that the history does not have are added,
    /// and what it knew of each replay too; nothing already here is replaced, so matches played since are kept.
    /// Returns how many matches came back.
    /// </summary>
    public int Restore(TrackerData backup)
    {
        var known = Data.Matches.Select(m => m.SessionId).ToHashSet();
        var added = 0;
        foreach (var match in backup.Matches.Where(m => known.Add(m.SessionId)))
        {
            Data.Matches.Add(match);
            added++;
        }
        foreach (var (file, verdict) in backup.ProcessedFiles)
        {
            // replays marked "skipped" by the reset get back what they were
            if (!Data.ProcessedFiles.TryGetValue(file, out var now) || now == Skipped)
            {
                Data.ProcessedFiles[file] = verdict;
            }
        }
        _store.SaveData(Data);
        return added;
    }

    /// <summary>
    /// After an analyzer change, forgets which replays were read (except the final verdicts) so the ones still on disk
    /// are read again and their matches replaced. Call before scanning. True when existing matches will be re-read.
    /// </summary>
    public bool UpgradeIfNeeded()
    {
        if (Data.AnalyzerVersion >= ReplayAnalyzer.CurrentVersion)
        {
            return false;
        }
        var reread = Data.ProcessedFiles.Where(e => e.Value is not (Skipped or OtherMode or OlderVersion)).Select(e => e.Key).ToList();
        foreach (var key in reread)
        {
            Data.ProcessedFiles.Remove(key);
        }
        Data.AnalyzerVersion = ReplayAnalyzer.CurrentVersion;
        _store.SaveData(Data);
        return Data.Matches.Count > 0;
    }

    // ---- Scanning ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads every replay that is ready. Several at once (replays from before) get a progress bar and a summary; a single
    /// one (the match that just ended) gets its own lines. Returns how many matches were added.
    /// </summary>
    /// <param name="ignoreQuietPeriod">Read finished replays right away (--once).</param>
    public int Scan(bool ignoreQuietPeriod = false)
    {
        var ready = ReadyReplays(ignoreQuietPeriod);
        if (ready.Count == 0)
        {
            return 0;
        }
        var results = ready.Count == 1 ? new List<ScanResult> { ReadOne(ready[0]) } : ReadBatch(ready);
        var added = results.Count(r => r.Match != null);
        if (added > 0 || ready.Count > 1)
        {
            StatsPage.WriteAll(_store, _config, Data);
        }
        return added;
    }

    /// <summary>
    /// The replays not handled yet that can be read now. Sets <see cref="HasPending"/> when some must still wait and
    /// <see cref="IsFinishing"/> when one of them is a match that just ended.
    /// </summary>
    private List<FileInfo> ReadyReplays(bool ignoreQuietPeriod)
    {
        HasPending = IsFinishing = false;
        var ready = _folder.NotHandled().ToList().Where(StillThere).ToLookup(replay =>
        {
            var canRead = _folder.IsReady(replay, ignoreQuietPeriod, out var justFinished);
            IsFinishing |= justFinished;
            return canRead;
        });
        HasPending = ready[false].Any();
        return ready[true].ToList();
    }

    private static bool StillThere(FileInfo replay)
    {
        replay.Refresh();
        return replay.Exists;
    }

    private ScanResult ReadOne(FileInfo file)
    {
        _console.Reading(file);
        var result = Record(file);
        _console.One(result);
        return result;
    }

    private List<ScanResult> ReadBatch(List<FileInfo> files)
    {
        var accountsBefore = Profiles.List(Data).Count;
        var progress = new ConsoleProgress(L.T("Analizando replays", "Analyzing replays"), files.Count);
        var results = new List<ScanResult>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            progress.Show(i, files[i].Name);
            StatsPage.Progress = new ScanProgress(i, files.Count);
            StatsPage.WriteAll(_store, _config, Data); // the stats page shows the loading card meanwhile
            Progressed?.Invoke(StatsPage.Progress);
            results.Add(Record(files[i]));
        }
        progress.Show(files.Count);
        progress.Erase();
        StatsPage.Progress = null;
        Progressed?.Invoke(null);
        _console.Batch(results);
        if (accountsBefore < 2 && Profiles.List(Data).Count >= 2)
        {
            NewProfilesFound = true;
        }
        return results;
    }

    /// <summary>Reads one replay and files its result in the history.</summary>
    private ScanResult Record(FileInfo file)
    {
        var key = FileKey(file);
        MatchRecord match;
        try
        {
            match = ReplayReading.Read(file, ReadLimit);
        }
        catch (Exception ex)
        {
            var reason = (ex as AggregateException)?.InnerException?.Message ?? ex.Message;
            ErrorLog.Write($"Unreadable replay: {file.FullName} ({file.Length} bytes, {file.LastWriteTime:yyyy-MM-dd HH:mm:ss})", ex);
            Mark(key, "error: " + reason);
            return new ScanResult(Error: $"{file.Name} : {reason}");
        }
        match.ReplayFile = file.Name;

        if (!match.IsBattleRoyale || !Profiles.IsSupportedVersion(match))
        {
            Data.Matches.RemoveAll(m => m.SessionId == match.SessionId); // also any earlier reading of it
            var battleRoyale = match.IsBattleRoyale;
            Mark(key, battleRoyale ? OlderVersion : OtherMode);
            return new ScanResult(Skipped: battleRoyale
                ? L.T("versión anterior a v43 (Capítulo 7 Mini Temporada)", "version prior to v43 (Chapter 7 Mini Season)")
                : L.T("no es una partida Battle Royale (creativo / otro modo)", "not a Battle Royale match (Creative / other mode)"));
        }

        var earlier = Data.Matches.Find(m => m.SessionId == match.SessionId);
        if (earlier != null && earlier.AnalyzerVersion >= match.AnalyzerVersion)
        {
            Mark(key, match.SessionId);
            return new ScanResult(Skipped: L.T("partida ya contada (misma partida, archivo renombrado)", "match already counted (same match, renamed file)"));
        }
        if (earlier != null)
        {
            match.AnalyzedAtUtc = earlier.AnalyzedAtUtc; // re-read by a newer analyzer: replaces the old record
            Data.Matches.Remove(earlier);
        }

        TreasureScreenMerge.Apply(match, Data.TreasureScreens); // names the gizmos, adds those the replay missed
        Data.Matches.Add(match);
        Mark(key, match.SessionId);
        if (_live.File == file.FullName)
        {
            _live.Clear(); // the final reading replaces the live estimate
        }
        AdoptFirstProfile(match);

        var unnamed = _store.RegisterPrizeIds(match.TreasureFinds.Where(f => f.PrizeId != null).Select(f => f.PrizeId!));
        return new ScanResult(match, Updated: earlier != null, NewPrizeIds: unnamed);
    }

    private void Mark(string key, string verdict)
    {
        Data.ProcessedFiles[key] = verdict;
        _store.SaveData(Data);
    }

    /// <summary>
    /// The very first match decides the profile; what was entered before any match (chests before FCT and their
    /// estimate, kept under "") moves to it.
    /// </summary>
    private void AdoptFirstProfile(MatchRecord match)
    {
        if (!string.IsNullOrEmpty(_config.Profile))
        {
            return;
        }
        var profile = Profiles.Key(match);
        _config.Profile = profile;
        if (_config.InitialChests.Remove("", out var chests))
        {
            _config.InitialChests.TryAdd(profile, chests);
        }
        if (_config.Estimates.Remove("", out var estimate) && !_config.Estimates.ContainsKey(profile))
        {
            estimate.ProfileKey = profile;
            _config.Estimates[profile] = estimate;
        }
        try
        {
            _store.SaveConfig(_config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // ---- The match being played -----------------------------------------------------------------------------------------

    /// <summary>Figures of the match in progress, when its replay can be read (never saved).</summary>
    public MatchRecord? LiveMatch
    {
        get => _live.Figures;
        private set => _live.Figures = value;
    }

    /// <summary>A replay is being recorded.</summary>
    public bool IsLive => _live.File != null;

    /// <summary>Follows the match in progress; true when what is shown should change.</summary>
    public bool UpdateLive() => _live.Refresh(_folder.NotHandled());

    /// <summary>Totals of the profile on screen, on the console.</summary>
    public void PrintSummary() => _console.Totals();
}
