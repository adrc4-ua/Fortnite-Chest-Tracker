using System.Security.Cryptography;
using System.Text;

namespace FCT.App;

/// <summary>Who FCT is: names, version, repository, where its exe is, and the names its processes meet under.</summary>
public static class AppInfo
{
    /// <summary>Long name, where there is room (window title, welcome, stats page).</summary>
    public const string Name = "Fortnite Chest Tracker";

    /// <summary>Short name, for sentences, menus and files.</summary>
    public const string ShortName = "FCT";

    /// <summary>The exe attached to every release.</summary>
    public const string ExeName = "FCT.exe";

    public const string Repo = "adrc4-ua/Fortnite-Chest-Tracker";
    public static string RepoUrl => $"https://github.com/{Repo}";
    public static string IssuesUrl => $"{RepoUrl}/issues/new";

    public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(1, 0, 0);

    /// <summary>"5.6.0".</summary>
    public static string VersionText => $"{Version.Major}.{Version.Minor}.{(Version.Build < 0 ? 0 : Version.Build)}";

    /// <summary>The running .exe, whatever its file name; null when run through "dotnet" (tests, development).</summary>
    /// <summary>
    /// A copy with another data folder (--data, a test environment): it never touches what belongs to the normal one —
    /// Windows' startup, its standby, the OBS port, files shared with it. Set once at start.
    /// </summary>
    public static bool Isolated { get; set; }

    public static string? ExePath
    {
        get
        {
            var process = Environment.ProcessPath;
            var isOwnExe = process != null
                && string.Equals(Path.GetExtension(process), ".exe", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetFileNameWithoutExtension(process), "dotnet", StringComparison.OrdinalIgnoreCase);
            return isOwnExe ? process : null;
        }
    }

    // ---- Names of the kernel objects shared by FCT's processes (per Windows session) -------------------------------------

    public const string StandbyMutex = @"Local\FCT.Standby";
    public const string StandbyStopEvent = @"Local\FCT.StandbyStop";

    /// <summary>
    /// The single-instance mutex. One FCT per user, whatever the data folder (two would show two overlays); only the
    /// automated tests set FCT_SEPARATE_INSTANCES=1 to get one per data folder.
    /// </summary>
    public static string InstanceMutexFor(string dataDir)
    {
        const string shared = @"Local\FCT.Instance";
        if (Environment.GetEnvironmentVariable("FCT_SEPARATE_INSTANCES") != "1")
        {
            return shared;
        }
        var folder = Path.GetFullPath(dataDir).TrimEnd('\\', '/').ToLowerInvariant();
        return $"{shared}.{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder)), 0, 8)}";
    }

    /// <summary>Signalled by a second launch so the running FCT (maybe only in the tray) brings its window up.</summary>
    public static string ShowEventFor(string dataDir) => InstanceMutexFor(dataDir).Replace(".Instance", ".Show");

    // ---- Legal texts -------------------------------------------------------------------------------------------------------

    /// <summary>FCT.exe --licenses: NOTICE.md, THIRD_PARTY_NOTICES.md and the GPL text, all embedded in the exe.</summary>
    public static int PrintLicenses()
    {
        foreach (var resource in new[] { "FCT.NOTICE.md", "FCT.THIRD_PARTY_NOTICES.md", "FCT.LICENSE" })
        {
            using var text = typeof(AppInfo).Assembly.GetManifestResourceStream(resource);
            if (text != null)
            {
                Console.WriteLine(new StreamReader(text).ReadToEnd());
                Console.WriteLine();
            }
        }
        Console.WriteLine(RepoUrl);
        return 0;
    }
}
