namespace FCT.App;

/// <summary>What FCT.exe was asked to do, from its command line.</summary>
internal sealed class LaunchOptions
{
    /// <summary>Options that print to the terminal (and so attach to it) instead of opening the window.</summary>
    private static readonly HashSet<string> TerminalOptions = new()
    {
        "--once", "--overlay-preview", "--diagnose", "--read-treasure-screen", "--capture-window", "--licenses", "--uninstall",
    };

    public string? DataFolder { get; private set; }
    public string? DemosFolder { get; private set; }
    public bool Once { get; private set; }
    public bool ResetConfig { get; private set; }

    /// <summary>Opened by "Open with Fortnite": closes itself after the game.</summary>
    public bool Auto { get; private set; }

    /// <summary>Restarted by the updater: waits for the previous instance to finish closing.</summary>
    public bool AfterUpdate { get; private set; }

    /// <summary>"--show": the window opens even when FCT starts by itself (a restart that had it in view).</summary>
    public bool Show { get; private set; }

    public string? OverlayPreview { get; private set; }

    /// <summary>--diagnose: the replay's signals side by side, for a file or a folder.</summary>
    public string? Diagnose { get; private set; }

    /// <summary>An option that runs on its own and ends the program (licenses, uninstall, screen tests).</summary>
    public Func<int>? RunAndExit { get; private set; }

    /// <summary>Output goes to the terminal that started the exe.</summary>
    public bool Terminal { get; private init; }

    public static LaunchOptions Parse(string[] args)
    {
        var options = new LaunchOptions { Terminal = args.Any(TerminalOptions.Contains) };
        var i = 0;
        string Value() => args[++i]; // a missing value fails like any other startup error
        for (; i < args.Length && options.RunAndExit == null; i++)
        {
            switch (args[i])
            {
                case "--data": options.DataFolder = Value(); break;
                case "--demos": options.DemosFolder = Value(); break;
                case "--lang": Value(); break; // older option: the language now follows Windows or Settings
                case "--once": options.Once = true; break;
                case "--reset-config": options.ResetConfig = true; break;
                case "--auto": options.Auto = true; break;
                case "--after-update": options.AfterUpdate = true; break;
                case "--show": options.Show = true; break;
                case "--overlay-preview": options.OverlayPreview = Value(); break;
                case "--diagnose": options.Diagnose = Value(); break;
                case "--licenses": options.RunAndExit = AppInfo.PrintLicenses; break;
                case "--uninstall": options.RunAndExit = () => OperatingSystem.IsWindows() ? Uninstaller.RunFromCommandLine() : 1; break;
                // Tests of the treasure screen reading.
                case "--read-treasure-screen":
                    var image = Value();
                    options.RunAndExit = () => PostMatchTools.ReadImage(image);
                    break;
                case "--capture-window":
                    var title = Value();
                    var output = i + 1 < args.Length ? Value() : null;
                    options.RunAndExit = () => PostMatchTools.CaptureWindow(title, output);
                    break;
            }
        }
        return options;
    }
}
