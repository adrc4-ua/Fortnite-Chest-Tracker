using System.Diagnostics;
using System.Runtime.Versioning;

namespace FCT.App;

/// <summary>
/// Copies of FCT older than this one, running or on disk. An exe is recognised by its version info (product
/// "Fortnite Chest Tracker" built as FCT.dll, or "ChestCounter" 1.x, the first name), never by its file name alone, and
/// only an older version is ever touched: never this exe, an equal or a newer one.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class OlderCopies
{
    private static readonly Version Current = Plain(AppInfo.Version);

    /// <summary>The FCT version of this exe; null when it is not an FCT exe (or cannot be read).</summary>
    public static Version? VersionOf(string exe)
    {
        try
        {
            if (!File.Exists(exe))
            {
                return null;
            }
            var info = FileVersionInfo.GetVersionInfo(exe);
            static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
            var fct = Is(info.ProductName, AppInfo.Name) && Is(info.OriginalFilename, "FCT.dll");
            var firstName = Is(info.ProductName, "ChestCounter") && Is(info.OriginalFilename, "ChestCounter.dll") && info.FileMajorPart == 1;
            return fct || firstName ? new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>An FCT exe older than this one, and not this very file.</summary>
    public static bool IsOlder(string exe) =>
        !SameFile(exe, AppInfo.ExePath) && VersionOf(exe) is { } version && version < Current;

    public static bool SameFile(string? a, string? b) =>
        a != null && b != null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>The exes of every copy <see cref="EndRunning"/> ended in this process, to delete them afterwards.</summary>
    public static List<string> Ended { get; } = new();

    /// <summary>
    /// Ends the running copies older than this one (the app, a standby waiting at logon, a launcher); true when there
    /// was any. Safe for the data: every version writes its files whole (temporary file, then replace).
    /// </summary>
    public static bool EndRunning()
    {
        var ended = Ended.Count;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || !NamedLikeFct(process.ProcessName) || ImageOf(process) is not { } exe || !IsOlder(exe))
                {
                    continue;
                }
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    Ended.Add(exe);
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // already gone, or not ours to end
                }
            }
        }
        return Ended.Count > ended;
    }

    /// <summary>
    /// A cheap first filter on the process name ("FCT", "FCT (1)" from a browser download, "ChestCounter"); the version
    /// info decides.
    /// </summary>
    private static bool NamedLikeFct(string name) =>
        name.StartsWith("FCT", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ChestCounter", StringComparison.OrdinalIgnoreCase);

    private static string? ImageOf(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null; // another user's, elevated, or already gone
        }
    }

    /// <summary>
    /// Deletes an older exe and what updates left next to it (*.old, an interrupted *.download). Something still in use
    /// stays: nothing here is needed, so a failure is never a problem.
    /// </summary>
    public static void Delete(string exe)
    {
        if (IsOlder(exe))
        {
            TryDeleteWithBinRecord(exe);
        }
        DeleteUpdateLeftovers(exe);
    }

    /// <summary>
    /// Ends the FCT processes, of any version but never this one, still running from an exe sent to the Recycle Bin
    /// (versions before the bin was watched kept running from there, unseen), and deletes that exe from the bin.
    /// </summary>
    public static void EndThrownAway()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || !NamedLikeFct(process.ProcessName) || ImageOf(process) is not { } exe
                    || !ExeLocation.IsInRecycleBin(exe) || SameFile(exe, ExeLocation.Current) || VersionOf(exe) == null)
                {
                    continue;
                }
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    TryDeleteWithBinRecord(exe);
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // already gone, or not ours to end
                }
            }
        }
    }

    /// <summary>Deletes a file and, inside the Recycle Bin, its record ("$I…"), so the bin does not list a file that is gone.</summary>
    private static void TryDeleteWithBinRecord(string path)
    {
        TryDelete(path);
        if (!File.Exists(path) && ExeLocation.RecycleBinRecord(path) is { } record)
        {
            TryDelete(record);
        }
    }

    /// <summary>"FCT.exe.old", "FCT.exe.20261112093000123.old" (older exes) and "FCT.exe.download" beside an exe.</summary>
    public static void DeleteUpdateLeftovers(string exe)
    {
        if (Path.GetDirectoryName(exe) is not { } folder || !Directory.Exists(folder))
        {
            return;
        }
        try
        {
            foreach (var leftover in Directory.EnumerateFiles(folder, Path.GetFileName(exe) + ".*"))
            {
                var retired = leftover.EndsWith(".old", StringComparison.OrdinalIgnoreCase) && VersionOf(leftover) != null;
                var download = leftover.EndsWith(".download", StringComparison.OrdinalIgnoreCase);
                if (retired || download)
                {
                    TryDelete(leftover);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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

    private static Version Plain(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
