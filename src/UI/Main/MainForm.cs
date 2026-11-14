using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// The main window, in the stats page's style:
/// <list type="bullet">
/// <item>a header with the name, the live badge and the profile;</item>
/// <item>the main card (chests opened, and the current or last match) with that match's details below;</item>
/// <item>the week's chart on the left; six figures and the buttons (like the page's menu) on the right;</item>
/// <item>when needed, the team card and a notice or update strip, for which the window grows.</item>
/// </list>
/// Scanning, the overlay and updates all run in <see cref="AppMonitor"/>; this only shows its state.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class MainForm : ThemedForm
{
    // ---- design grid (592 wide; everything scales with the window) --------------------------------------------------
    private const int GridLeft = 16, ContentWidth = 560, RightEdge = GridLeft + ContentWidth, GridTop = 68;
    private const int ColumnWidth = 270, RightX = GridLeft + ColumnWidth + 12, RightWidth = RightEdge - RightX;
    private const int HeroHeight = 100, DetailY = GridTop + HeroHeight + 8, DetailHeight = 44, WeekY = DetailY + DetailHeight + 8;
    private const int TileWidth = (RightWidth - 8) / 2, TileHeight = 58, TileGap = 8;
    private const int NavY = GridTop + 3 * TileHeight + 2 * TileGap + 12, NavHeight = 32, NavGap = 5;
    private const int GridBottom = NavY + 3 * NavHeight + 2 * NavGap, WeekHeight = GridBottom - WeekY;
    private const int DesignWidth = 592, PlainHeight = GridBottom + 16;
    private const int TeamY = GridBottom + 10, TeamHeight = 78, TeamDesignHeight = TeamY + TeamHeight + 16;
    private const int StripHeight = 40, StripY = GridBottom + 10, StripYUnderTeam = TeamY + TeamHeight + 10, StripMargin = 16;

    private readonly AppMonitor _monitor;
    private readonly Store _store;
    private readonly ProportionalLayout _layout;
    private readonly System.Windows.Forms.Timer _rescale = new() { Interval = 50 };
    private readonly TrayIcon _tray;

    // header
    private readonly Card _badge;
    private readonly FitLabel _badgeText, _profileCaption, _profileName;
    private readonly IconTile _profileIcon;
    private readonly ComboBox _profiles;
    // main card and details
    private readonly FitLabel _total, _totalNote, _match, _matchCaption, _matchNote, _matchDetails;
    // figures
    private readonly FitLabel _today, _todayCaption, _bestDay, _bestDayCaption, _average, _bestMatch, _matches, _treasures;
    // week, buttons, team and the bottom strip
    private readonly DailyChart _week;
    private readonly FitLabel _weekNote;
    private readonly PillButton _statsButton, _overlayButton, _settingsButton, _update;
    private readonly TeamCard _team;
    private readonly Card _noticeCard;
    private readonly FitLabel _notice;

    private MonitorState? _state;
    private bool _fullscreenMessagePending;
    private bool _exiting, _settingsOpen, _startHandled, _keepOpenOffered;

    public MainForm(AppMonitor monitor, Store store)
    {
        _monitor = monitor;
        _store = store;
        Text = AppInfo.Name;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(440, 300);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(DesignWidth, PlainHeight);
        Icon = AppIcon.Load();

        // ---- header: the name like the page's side bar on the left, the profile on the right, the badge in between
        FitLabel.Add(this, "F O R T N I T E", 6.75f, FontStyle.Bold, Theme.GoldBase, new Point(GridLeft, 13), new Size(130, 13));
        FitLabel.Add(this, "Chest Tracker", 13.5f, FontStyle.Bold, Theme.GoldBase, new Point(GridLeft - 2, 25), new Size(190, 26), figure: true, gold: true);
        _profileIcon = new IconTile { IconSvg = Icons.Person, Location = new Point(RightEdge - 34, 15), Size = new Size(34, 34) };
        Controls.Add(_profileIcon);
        _profileCaption = FitLabel.Add(this, L.T("P E R F I L", "P R O F I L E"), 6.75f, FontStyle.Bold, Theme.Muted, new Point(RightEdge - 44 - 200, 13), new Size(200, 13), ContentAlignment.TopRight);
        _profileName = FitLabel.Add(this, "", 10.5f, FontStyle.Bold, Theme.Ink, new Point(RightEdge - 44 - 200, 27), new Size(200, 22), ContentAlignment.TopRight);
        _profiles = ProfilePicker(new Point(RightEdge - 44 - 190, 26), new Size(190, 24));
        Controls.Add(_profiles);
        _badge = new Card { Location = new Point(GridLeft + ContentWidth / 2 - 60, 20), Size = new Size(120, 24), Radius = 12, BackColor = Theme.Card, Edge = Color.FromArgb(90, Theme.Mint), Visible = false };
        _badgeText = FitLabel.Add(_badge, "", 7.75f, FontStyle.Bold, Theme.Mint, new Point(0, 0), new Size(120, 24), ContentAlignment.MiddleCenter);
        Controls.Add(_badge);
        Controls.Add(new Panel { BackColor = Theme.Line, Location = new Point(GridLeft, 58), Size = new Size(ContentWidth, 1) });

        // ---- main card: two equal halves, each a centred title, gold figure and note
        var hero = new Card { Location = new Point(GridLeft, GridTop), Size = new Size(ColumnWidth, HeroHeight), Hero = true };
        Controls.Add(hero);
        const int Half = ColumnWidth / 2;
        FitLabel.Add(hero, L.T("Cofres abiertos", "Chests opened"), 8.75f, FontStyle.Bold, Theme.Ink, new Point(8, 13), new Size(Half - 16, 17), ContentAlignment.TopCenter);
        _total = FitLabel.Add(hero, "0", 26f, FontStyle.Bold, Theme.GoldBase, new Point(8, 30), new Size(Half - 16, 44), ContentAlignment.MiddleCenter, figure: true, gold: true, deep: true);
        _totalNote = FitLabel.Add(hero, "", 7.75f, FontStyle.Regular, Theme.Muted, new Point(8, 75), new Size(Half - 16, 15), ContentAlignment.TopCenter);
        hero.Controls.Add(new Panel { BackColor = Theme.Line, Location = new Point(Half, 20), Size = new Size(1, HeroHeight - 40) });
        _matchCaption = FitLabel.Add(hero, L.T("Última partida", "Last match"), 8.75f, FontStyle.Bold, Theme.Ink, new Point(Half + 8, 13), new Size(Half - 16, 17), ContentAlignment.TopCenter);
        _match = FitLabel.Add(hero, "—", 26f, FontStyle.Bold, Theme.GoldBase, new Point(Half + 8, 30), new Size(Half - 16, 44), ContentAlignment.MiddleCenter, figure: true, gold: true, deep: true);
        _matchNote = FitLabel.Add(hero, "", 7.75f, FontStyle.Regular, Theme.Muted, new Point(Half + 8, 75), new Size(Half - 16, 15), ContentAlignment.TopCenter);

        // that match's details: the icon on the left, the text centred with the same margin on both sides
        var details = new Card { Location = new Point(GridLeft, DetailY), Size = new Size(ColumnWidth, DetailHeight), Radius = 14 };
        Controls.Add(details);
        details.Controls.Add(new IconTile { IconSvg = Icons.Matches, Location = new Point(9, 9), Size = new Size(26, 26) });
        _matchDetails = FitLabel.Add(details, "", 8.5f, FontStyle.Bold, Theme.Champ, new Point(42, 0), new Size(ColumnWidth - 84, DetailHeight), ContentAlignment.MiddleCenter);

        // ---- this week
        var week = new Card { Location = new Point(GridLeft, WeekY), Size = new Size(ColumnWidth, WeekHeight) };
        Controls.Add(week);
        week.Controls.Add(new SectionTitle { Text = L.T("Esta semana", "This week"), Location = new Point(12, 7), Size = new Size(110, 20) });
        _weekNote = FitLabel.Add(week, "", 7.75f, FontStyle.Regular, Theme.Muted, new Point(ColumnWidth - 14 - 140, 9), new Size(140, 16), ContentAlignment.MiddleRight);
        _week = new DailyChart { Location = new Point(8, 30), Size = new Size(ColumnWidth - 16, WeekHeight - 36), Font = Theme.Text(8f) };
        week.Controls.Add(_week);

        // ---- figures, 2 × 3
        _today = Figure(0, 0, L.T("Hoy", "Today"), Icons.Today, out _todayCaption);
        _bestDay = Figure(1, 0, L.T("Mejor día", "Best day"), Icons.Day, out _bestDayCaption);
        _average = Figure(0, 1, L.T("Media por partida", "Average per match"), Icons.Average, out _);
        _bestMatch = Figure(1, 1, L.T("Récord", "Record"), Icons.Record, out _);
        _matches = Figure(0, 2, L.T("Partidas", "Matches"), Icons.Matches, out _);
        _treasures = Figure(1, 2, L.T("Tesoros", "Treasures"), Icons.Treasure, out _);

        // ---- buttons, like the page's menu
        _statsButton = MenuButton(L.T("Estadísticas", "Stats"), Icons.Summary, NavY);
        _statsButton.Click += (_, _) => Open(_monitor.StatsTarget);
        _overlayButton = MenuButton(L.T("Overlay", "Overlay"), Icons.Overlay, NavY + NavHeight + NavGap);
        _overlayButton.Click += (_, _) => _monitor.ToggleOverlay();
        _settingsButton = MenuButton(L.T("Configuración", "Settings"), Icons.Settings, NavY + 2 * (NavHeight + NavGap));
        _settingsButton.Click += (_, _) => OpenSettings();

        // ---- team (with teammates on) and the bottom strip: a notice, or the update button
        _team = new TeamCard(new Point(GridLeft, TeamY), ContentWidth, TeamHeight);
        Controls.Add(_team.Card);
        _noticeCard = new Card { Location = new Point(GridLeft, StripY), Size = new Size(ContentWidth, StripHeight), Radius = 14, Edge = Theme.GoldLine, Visible = false };
        _noticeCard.Controls.Add(new IconTile { IconSvg = Icons.Info, Location = new Point(8, 7), Size = new Size(26, 26) });
        _notice = FitLabel.Add(_noticeCard, "", 8.5f, FontStyle.Regular, Theme.GoldHi, new Point(42, 0), new Size(ContentWidth - 42 - 14, StripHeight), ContentAlignment.MiddleLeft);
        _noticeCard.Click += (_, _) => KeepOpenIfOffered();
        _notice.Click += (_, _) => KeepOpenIfOffered();
        Controls.Add(_noticeCard);
        _update = new PillButton
        {
            Location = new Point(GridLeft, StripY), Size = new Size(ContentWidth, StripHeight), Radius = 14, Visible = false, IconSvg = Icons.Download,
            BackColor = Theme.Accent, HoverColor = Theme.AccentHover, Font = Theme.Text(9.25f, FontStyle.Bold),
        };
        _update.Click += (_, _) => _monitor.InstallUpdate();
        Controls.Add(_update);

        _tray = new TrayIcon(Icon, ShowFromTray, _monitor.ToggleOverlay, () => Open(_monitor.StatsTarget),
            () => { ShowFromTray(); OpenSettings(); }, ShowAbout, ExitApp);

        _layout = new ProportionalLayout(this, DesignWidth, PlainHeight);
        _layout.Capture();
        _rescale.Tick += (_, _) => { _rescale.Stop(); _layout.Apply(); };
        monitor.Changed += OnChanged;
        monitor.ExclusiveFullscreen += () => OnWindowThread(WarnAboutExclusiveFullscreen);
        Activated += (_, _) => ShowFullscreenMessageIfPending();
        // hidden in the tray = in the background: the app then closes by itself when Fortnite closes
        VisibleChanged += (_, _) => _monitor.InBackground = !Visible;
    }

    /// <summary>
    /// Start with the overlay only: the window is created (to receive the state) but not shown, neither in front nor on
    /// the taskbar; the tray icon or opening the app again brings it back. Program decides (Fortnite open, not first use).
    /// </summary>
    internal bool StartInBackground;

    /// <summary>Closes the app for real (menu "Salir", the overlay's ✕, Fortnite closing, an update).</summary>
    internal void ExitApp()
    {
        ShownAtExit = Visible && WindowState != FormWindowState.Minimized;
        _exiting = true;
        Close();
    }

    /// <summary>The window was in view when the app closed (a restart into a new version shows it again).</summary>
    internal bool ShownAtExit { get; private set; }

    /// <summary>Brings the window to the front (tray icon, its menu, or the app opened again).</summary>
    internal void ShowFromTray()
    {
        _startHandled = true;
        if (!Visible)
        {
            Show();
        }
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
        BringToFront();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (value && StartInBackground && !_startHandled)
        {
            // Application.Run would show it
            _startHandled = true;
            if (!IsHandleCreated)
            {
                CreateHandle();
            }
            base.SetVisibleCore(false);
            _monitor.InBackground = true;
            return;
        }
        base.SetVisibleCore(value);
    }

    /// <summary>
    /// The window's ✕: with Fortnite open the app goes to the background (tray icon) and closes with Fortnite;
    /// otherwise it closes normally.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing && OperatingSystem.IsWindows() && FortniteWatch.IsFortniteRunning())
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _tray.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState != FormWindowState.Minimized)
        {
            _rescale.Stop();
            _rescale.Start();
        }
    }

    private void OpenSettings()
    {
        if (_settingsOpen)
        {
            return;
        }
        _settingsOpen = true;
        try
        {
            using var dialog = new SettingsForm(_monitor, _store);
            dialog.ShowDialog(this);
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void KeepOpenIfOffered()
    {
        if (_keepOpenOffered)
        {
            _monitor.KeepOpen();
        }
    }

    /// <summary>
    /// GPL-3.0's appropriate legal notices (and NOTICE.md's credit): copyright, no warranty, the licence and where to
    /// read it. From the tray menu; "Ver licencia" opens NOTICE.md on GitHub.
    /// </summary>
    private void ShowAbout()
    {
        var read = InfoDialog.Confirm(Visible ? this : null, $"{AppInfo.Name} {AppInfo.VersionText}",
            L.T("Copyright © 2026 Adrián Ruiz", "Copyright © 2026 Adrián Ruiz") + "\n" + AppInfo.RepoUrl,
            new[]
            {
                new InfoPoint(Icons.Info, Theme.GoldHi, L.T("Software libre: GPL-3.0", "Free software: GPL-3.0"),
                    L.T("Puedes usarlo, modificarlo y compartirlo con esa licencia y sus condiciones (mencionar al autor y este repositorio). Las versiones hasta la 4.4.1 se publicaron con MIT.",
                        "You can use, change and share it under that license and its terms (credit the author and this repository). Versions up to 4.4.1 were released under MIT.")),
                new InfoPoint(Icons.Warning, Theme.GoldHi, L.T("Sin garantía", "No warranty"),
                    L.T("Se ofrece tal cual, sin garantía de ningún tipo.", "It is provided as is, without any warranty.")),
                new InfoPoint(Icons.Folder, Theme.GoldHi, L.T("Código de terceros", "Third-party code"),
                    L.T("Inspirado en ChestCounter, de xdoofy92. Usa FortniteReplayDecompressor (© Shiqan, MIT) y .NET (MIT). No afiliado a Epic Games.",
                        "Inspired by ChestCounter, by xdoofy92. Uses FortniteReplayDecompressor (© Shiqan, MIT) and .NET (MIT). Not affiliated with Epic Games.")),
            },
            L.T("Ver licencia", "View license"), L.T("Cerrar", "Close"), Icons.Info);
        if (read)
        {
            Open(AppInfo.RepoUrl + "/blob/main/NOTICE.md");
        }
    }

    // ---- Fortnite in exclusive fullscreen ----------------------------------------------------------------------------

    private static string FullscreenTitle => L.T("El overlay no se ve en Pantalla completa", "The overlay is hidden in Fullscreen");

    /// <summary>
    /// A Windows notification now (Windows holds it while a game is in fullscreen, and "Do not disturb" hides it), and
    /// FCT's own message the next time the player looks at the window (at once if it is in front already).
    /// </summary>
    private void WarnAboutExclusiveFullscreen()
    {
        _tray.ShowTip(FullscreenTitle, L.T("Pon Fortnite en Pantalla completa en ventana: Ajustes → Vídeo → Modo de ventana.",
            "Set Fortnite to Windowed Fullscreen: Settings → Video → Window Mode."));
        _fullscreenMessagePending = true;
        if (Visible && ContainsFocus)
        {
            ShowFullscreenMessageIfPending();
        }
    }

    /// <summary>Once per run of FCT: the first time its window comes to the front after Fortnite was found in fullscreen.</summary>
    private void ShowFullscreenMessageIfPending()
    {
        if (!_fullscreenMessagePending)
        {
            return;
        }
        _fullscreenMessagePending = false;
        BeginInvoke(() => InfoDialog.Confirm(this, FullscreenTitle,
            L.T("Fortnite está en Pantalla completa, y en ese modo Windows no deja que se vea nada encima del juego.",
                "Fortnite is in Fullscreen, and in that mode Windows lets nothing show over the game."),
            new[]
            {
                new InfoPoint(Icons.Settings, Theme.GoldHi, L.T("Cámbialo en Fortnite", "Change it in Fortnite"),
                    L.T("Ajustes → Vídeo → Modo de ventana → Pantalla completa en ventana. Se ve igual y rinde igual.",
                        "Settings → Video → Window Mode → Windowed Fullscreen. It looks and runs the same.")),
                new InfoPoint(Icons.Overlay, Theme.GoldHi, L.T("El overlay vuelve solo", "The overlay comes back by itself"),
                    L.T("En cuanto lo cambies, el overlay se verá encima del juego.", "As soon as you change it, the overlay shows over the game.")),
            },
            L.T("Entendido", "Got it"), null, Icons.Warning));
    }

    /// <summary>Runs on the window's thread when the window exists (events come from the monitor thread).</summary>
    private void OnWindowThread(Action action)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }
        try
        {
            BeginInvoke(action);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    /// <summary>Opens a page or file with its program; nothing happens when it does not exist yet or there is no browser.</summary>
    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
            // the stats page is not written yet, or there is no browser: nothing to do
        }
    }

    // ---- building blocks ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A figure of the grid (like the page's): its title on top, its icon on the right, the figure in gold. The figure
    /// starts under the icon (painted over it, it would hide it). Returns the figure; <paramref name="caption"/> is the title.
    /// </summary>
    private FitLabel Figure(int column, int row, string title, string icon, out FitLabel caption)
    {
        var card = new Card { Location = new Point(RightX + column * (TileWidth + TileGap), GridTop + row * (TileHeight + TileGap)), Size = new Size(TileWidth, TileHeight), Radius = 14 };
        Controls.Add(card);
        // title and figure start on the same line, with the same margin as the icon on the right; neither label reaches
        // the icon's column (a see-through label repaints what is under it)
        const int Margin = 12, Icon = 20, Gap = 4;
        var iconX = TileWidth - Margin - Icon;
        caption = FitLabel.Add(card, title, 8f, FontStyle.Bold, Theme.Muted, new Point(Margin - 1, 9), new Size(iconX - Gap - (Margin - 1), 15));
        card.Controls.Add(new IconTile { IconSvg = icon, Location = new Point(iconX, 9), Size = new Size(Icon, Icon) });
        return FitLabel.Add(card, "—", 15f, FontStyle.Bold, Theme.GoldBase, new Point(Margin, 24), new Size(iconX - Gap - Margin, 25), figure: true, gold: true);
    }

    /// <summary>A button like an entry of the page's menu: icon and text on the left.</summary>
    private PillButton MenuButton(string text, string icon, int y)
    {
        var button = new PillButton
        {
            Text = text, IconSvg = icon, LeftAligned = true, Location = new Point(RightX, y), Size = new Size(RightWidth, NavHeight), Radius = 11,
            Font = Theme.Text(9f, FontStyle.Bold), BackColor = Theme.Card, HoverColor = Theme.Panel,
        };
        Controls.Add(button);
        return button;
    }
}
