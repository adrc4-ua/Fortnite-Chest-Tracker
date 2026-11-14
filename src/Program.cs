using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Entry point. [STAThread]: the clipboard and the dialogs need an STA thread (top-level statements would start on
/// an MTA one, and "Copy" would silently fail).
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // "Open with Fortnite" helpers: no console, no window, nothing else.
        if (args.Contains("--on-game") || args.Contains("--standby"))
        {
            return OperatingSystem.IsWindows() ? RunWatcher(args) : 1;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // before the first window (message boxes included)
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var options = LaunchOptions.Parse(args);
        PrepareConsole(options.Terminal);
        ReportCrashes(options.Terminal);
        if (options.RunAndExit != null)
        {
            return options.RunAndExit();
        }

        // Windows startup is only touched for the normal data folder, never for a test one (--data).
        var defaultFolder = options.DataFolder == null;
        var dataDir = options.DataFolder ?? Store.DefaultFolder();
        var store = new Store(dataDir);
        ErrorLog.FilePath = store.ErrorLogPath;

        using var instance = new SingleInstance(dataDir);
        var tool = options.OverlayPreview != null || options.Diagnose != null;
        if (!tool && !instance.TryTake(options.AfterUpdate ? TimeSpan.FromSeconds(20) : TimeSpan.Zero) && !TakeOverFromOlder(instance, defaultFolder))
        {
            if (!options.Auto) // opened with Fortnite while already open: nothing to say
            {
                instance.PointToRunning(options.Terminal);
            }
            return 0;
        }

        var config = LoadConfig(store, options, out var firstRun);
        if (options.OverlayPreview != null)
        {
            return CommandLineTools.OverlayPreview(store, config, options.OverlayPreview);
        }
        if (options.Diagnose != null)
        {
            return CommandLineTools.Diagnose(store, options.Diagnose);
        }

        // before the history is loaded: an older copy still open must not write to it afterwards
        if (OperatingSystem.IsWindows() && !options.Once)
        {
            Leftovers.CleanOnce(config, store, defaultFolder);
            if (defaultFolder && AppInfo.ExePath is { } exe)
            {
                config.LastExe = exe;
            }
        }

        LowerPriority();
        store.LoadPrizeNames();
        var data = store.LoadData();
        var tracker = new Tracker(store, config, data);
        tracker.UpgradeIfNeeded(); // before any scan, or replays would be read twice
        if (options.Once)
        {
            return CommandLineTools.Once(store, config, data, tracker);
        }

        if (firstRun && FirstRun.Run(config, tracker, instance, dataDir, defaultFolder) != FirstRun.Outcome.Continue)
        {
            return 0; // closed (nothing saved: next time is a first run again) or updated (the new exe takes over)
        }
        store.SaveConfig(config);
        if (OperatingSystem.IsWindows())
        {
            Updater.CleanupOld();
            if (config.StartWithFortnite && defaultFolder)
            {
                // Follows the exe if it moved or was updated and clears older startup entries; in the background, as it
                // asks the task scheduler and must not delay the window.
                _ = Task.Run(() => FortniteWatch.Apply(true, dataDir));
            }
        }
        return RunWindow(args, options, firstRun, store, config, data, tracker, instance, dataDir);
    }

    /// <summary>
    /// The FCT already open is an older version (a newer exe was downloaded and opened): it is closed and this one takes
    /// its place. Only for the normal data folder, never from a test one.
    /// </summary>
    private static bool TakeOverFromOlder(SingleInstance instance, bool defaultFolder)
    {
        var eligible = defaultFolder || Environment.GetEnvironmentVariable("FCT_TEST_CLEANUP") == "1";
        return eligible && OperatingSystem.IsWindows() && OlderCopies.EndRunning() && instance.TryTake(TimeSpan.FromSeconds(5));
    }

    private static int RunWatcher(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }
        var at = Array.IndexOf(args, "--data");
        var dataDir = at >= 0 && at + 1 < args.Length ? args[at + 1] : Store.DefaultFolder();
        return args.Contains("--on-game") ? FortniteWatch.RunOnGameStart(dataDir) : FortniteWatch.RunStandby(dataDir);
    }

    /// <summary>
    /// Command line options print into the terminal that started the exe; otherwise FCT is a window and its console (where
    /// the analysis logs) stays hidden.
    /// </summary>
    private static void PrepareConsole(bool terminal)
    {
        if (OperatingSystem.IsWindows())
        {
            NativeConsole.Ensure(attachToParent: terminal);
            if (!terminal)
            {
                NativeConsole.Hide();
            }
        }
        Console.OutputEncoding = Encoding.UTF8;
    }

    /// <summary>A crash leaves a trace (errors.log) and a message, never a silent disappearance.</summary>
    private static void ReportCrashes(bool terminal)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            ErrorLog.Write("Unexpected error", e.ExceptionObject);
            if (terminal)
            {
                Console.WriteLine(L.T("Error inesperado: ", "Unexpected error: ") + e.ExceptionObject);
                return;
            }
            MessageBox.Show(L.T("Error inesperado. Los detalles están en errors.log, en la carpeta de datos.",
                    "Unexpected error. The details are in errors.log, in the data folder.") + "\n\n" + e.ExceptionObject,
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
    }

    /// <summary>config.json (none, or --reset-config: a first run), with the newer defaults and the replays folder.</summary>
    private static Config LoadConfig(Store store, LaunchOptions options, out bool firstRun)
    {
        var config = options.ResetConfig ? null : store.LoadConfig();
        firstRun = config == null;
        config ??= new Config { DefaultsVersion = Config.CurrentDefaults };
        config.ApplyNewDefaults();
        config.MoveLegacyEstimate();
        L.Use(config.Language);
        if (options.DemosFolder != null)
        {
            config.DemosFolder = options.DemosFolder;
        }
        if (string.IsNullOrWhiteSpace(config.DemosFolder))
        {
            config.DemosFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
        }
        return config;
    }

    /// <summary>
    /// The window until FCT closes. Started by Fortnite (or restarting after an update with Fortnite open) only the overlay
    /// shows and the window waits in the notification area; opened by the player, or on a first run, the window opens.
    /// Afterwards: uninstall, or restart (new version, or a language change).
    /// </summary>
    private static int RunWindow(string[] args, LaunchOptions options, bool firstRun, Store store, Config config, TrackerData data, Tracker tracker, SingleInstance instance, string dataDir)
    {
        var fortniteOpen = options.Auto || (OperatingSystem.IsWindows() && FortniteWatch.IsFortniteRunning());
        if (fortniteOpen)
        {
            config.OverlayVisible = true;
        }
        var monitor = new AppMonitor(store, config, data, tracker, dataDir) { AutoMode = options.Auto };
        var form = new MainForm(monitor, store);
        monitor.CloseRequested += () => OnWindowThread(form, form.ExitApp);

        using (instance.OnShowRequested(() =>
               {
                   if (OnWindowThread(form, form.ShowFromTray))
                   {
                       monitor.SetNotice(L.F("{0} ya estaba abierto: solo puede haber uno a la vez.", "{0} was already open: there can only be one at a time.", AppInfo.ShortName),
                           seconds: 5); // a quick reminder: the window it asked for is already in front
                   }
               }))
        {
            monitor.Start();
            // Only when FCT opens by itself (with the game, or restarting after an update) does it wait in the tray with the
            // overlay alone; opened by the player, the window always shows (the game is never in front at that moment).
            var openedByItself = options.Auto || options.AfterUpdate;
            form.StartInBackground = !firstRun && openedByItself && fortniteOpen && monitor.OverlayVisible;
            Application.Run(form);
        }
        monitor.Stop();

        if (monitor.UninstallRequested && OperatingSystem.IsWindows())
        {
            instance.Release();
            Uninstaller.RemoveTraces();
            Uninstaller.DeleteSelfAfterExit();
            return 0;
        }
        if ((monitor.RestartAfterUpdate || monitor.RestartRequested) && AppInfo.ExePath is { } exe)
        {
            instance.Release();
            var again = args.Where(a => a != "--after-update").Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
            if (monitor.RestartAfterUpdate)
            {
                again = again.Append("--after-update"); // the new version announces itself
            }
            Process.Start(new ProcessStartInfo(exe, string.Join(" ", again)) { UseShellExecute = true })?.Dispose();
        }
        return 0;
    }

    /// <summary>Runs on the window's thread; false when the window does not exist (yet, or any more).</summary>
    private static bool OnWindowThread(Form form, Action action)
    {
        try
        {
            form.BeginInvoke(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Below-normal priority and Windows' efficiency mode: the game always comes first.</summary>
    private static void LowerPriority()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            using var self = Process.GetCurrentProcess();
            self.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
        try
        {
            EfficiencyMode.Enable();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or EntryPointNotFoundException or DllNotFoundException)
        {
        }
    }
}
