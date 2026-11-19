using System.Diagnostics;
using System.Runtime.Versioning;

namespace FCT.App;

/// <summary>
/// The first time each version opens, it clears what earlier versions may have left, from 1.0.0 on: older copies still
/// running (an FCT, a standby at logon, a launcher), any copy still running from the Recycle Bin, their exes and update
/// leftovers wherever the startup entries, the
/// last start or this exe's folder point, startup entries of other names, and temporary files in the data folder and
/// in Windows' temporary folder. The player's data (history, settings, prize names, backups) is never touched; nor is
/// the folder of the 1.0.0 version (%APPDATA%\ChestCounter), which holds that version's history.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Leftovers
{
    /// <summary>Files FCT writes in the data folder through a temporary "*.tmp" that a crash could leave behind.</summary>
    private static readonly string[] WrittenWhole = { "data.json", "config.json", "treasures.json", "estadisticas.html", "overlay.html", "matches.csv" };

    /// <summary>
    /// Once per version (remembered in config.json). Windows' side (processes, exes, startup entries) only for the normal
    /// data folder: a test folder (--data) never touches the PC, unless FCT_TEST_CLEANUP=1 asks for it.
    /// </summary>
    public static void CleanOnce(Config config, Store store, bool defaultFolder)
    {
        if (config.CleanedFor == AppInfo.VersionText)
        {
            return;
        }
        DataFolder(store);
        if (defaultFolder || Environment.GetEnvironmentVariable("FCT_TEST_CLEANUP") == "1")
        {
            OlderVersions(config);
            TemporaryFolder();
        }
        config.CleanedFor = AppInfo.VersionText;
    }

    /// <summary>Older copies and everything they left; the startup entries are rewritten afterwards for this exe.</summary>
    private static void OlderVersions(Config config)
    {
        // where earlier copies live: read before the startup entries are pointed at this exe
        var exes = new List<string>();
        if (StartupEntries.TaskExe() is { } task)
        {
            exes.Add(task);
        }
        exes.AddRange(StartupEntries.RunEntryExes());
        OlderCopies.EndThrownAway();
        OlderCopies.EndRunning();
        exes.AddRange(OlderCopies.Ended); // also those ended at start, to take their place
        if (config.LastExe is { Length: > 0 } last)
        {
            exes.Add(last);
        }
        exes.AddRange(BesideThisExe());

        foreach (var exe in exes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            OlderCopies.Delete(exe);
        }
        // v1.0.0's standby, should it have outlived the above (it answers to its own stop signal)
        if (EventWaitHandle.TryOpenExisting(@"Local\ChestCounter.StandbyStop", out var stop))
        {
            using (stop)
            {
                stop.Set();
            }
        }
        if (!config.StartWithFortnite)
        {
            FortniteWatch.Apply(false, Store.DefaultFolder()); // off: no task, entry or standby of any version stays
        }
        else
        {
            StartupEntries.RemoveRunEntries(StartupEntries.AllRunEntries.Where(entry => entry != StartupEntries.StandbyEntry)); // other names; the task is rewritten at start
        }
    }

    /// <summary>Other FCT exes in this exe's folder ("FCT (1).exe" next to "FCT.exe" in Downloads, an old "ChestCounter.exe").</summary>
    private static IEnumerable<string> BesideThisExe()
    {
        if (AppInfo.ExePath is not { } exe || Path.GetDirectoryName(exe) is not { } folder)
        {
            return Array.Empty<string>();
        }
        try
        {
            return Directory.EnumerateFiles(folder, "FCT*.exe").Concat(Directory.EnumerateFiles(folder, "ChestCounter*.exe")).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Half-written files of a crash, and the stats page's earlier name.</summary>
    private static void DataFolder(Store store)
    {
        foreach (var path in WrittenWhole.Select(name => Path.Combine(store.Folder, name + ".tmp")).Append(store.LegacyStatsPath))
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// In %TEMP%: task definitions a crash left ("fct-task-*.xml") and the native libraries older exes unpacked
    /// (.net\FCT…\&lt;build&gt;, .net\ChestCounter…), except those this process has loaded.
    /// </summary>
    private static void TemporaryFolder()
    {
        var temp = Path.GetTempPath();
        try
        {
            foreach (var file in Directory.EnumerateFiles(temp, "fct-task-*.xml"))
            {
                TryDelete(file);
            }
            var unpacked = Path.Combine(temp, ".net");
            if (!Directory.Exists(unpacked))
            {
                return;
            }
            if (LoadedFolders() is not { } inUse)
            {
                return; // unknown which ones this process uses: keep them all
            }
            var apps = Directory.EnumerateDirectories(unpacked, "FCT*").Concat(Directory.EnumerateDirectories(unpacked, "ChestCounter*"));
            foreach (var build in apps.SelectMany(Directory.EnumerateDirectories).ToList())
            {
                if (!inUse.Any(folder => folder.StartsWith(build.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))
                {
                    TryDeleteFolder(build);
                }
            }
            foreach (var app in Directory.EnumerateDirectories(unpacked, "FCT*").Concat(Directory.EnumerateDirectories(unpacked, "ChestCounter*")).ToList())
            {
                if (!Directory.EnumerateFileSystemEntries(app).Any())
                {
                    TryDeleteFolder(app);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The folders of every module this process has loaded, each ending in "\".</summary>
    private static List<string>? LoadedFolders()
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            return self.Modules.Cast<ProcessModule>()
                .Select(module => Path.GetDirectoryName(module.FileName) is { } folder ? folder.TrimEnd('\\') + "\\" : null)
                .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFolder(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
