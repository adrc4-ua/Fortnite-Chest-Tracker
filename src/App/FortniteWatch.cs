using System.Diagnostics;
using System.Runtime.Versioning;

namespace FCT.App;

/// <summary>
/// "Open with Fortnite" without anything running between matches. Each time the game starts, its anti-cheat loads a
/// driver and Windows logs it; a scheduled task of the user (<see cref="StartupEntries"/>, no admin rights needed)
/// reacts to that entry by running a short launcher (<see cref="RunOnGameStart"/>) that opens FCT once the game is up
/// and ends. FCT closes after Fortnite does. Where no task can be created, <see cref="StandbyWatcher"/> takes over.
/// Nothing here reads anything from the game.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FortniteWatch
{
    /// <summary>How long the launcher waits for the game: the driver comes ~10 s before it, more on slow disks or updates.</summary>
    private static readonly TimeSpan LauncherPatience = TimeSpan.FromMinutes(3);

    public static bool IsGameProcess(int pid) => GameProcess.IsGame(pid);
    public static bool IsFortniteRunning() => GameProcess.IsRunning();
    public static bool TaskExists() => StartupEntries.TaskExists();

    public static int RunStandby(string dataDir) => StandbyWatcher.Run(dataDir, OpenFct);
    public static void StopStandby() => StandbyWatcher.Stop();

    /// <summary>
    /// Turns the option on or off. Run at every start as well, so the task follows the exe when it moves or updates and
    /// older startup entries go away. False when it cannot be set up (FCT not running from its exe).
    /// </summary>
    public static bool Apply(bool enabled, string dataDir)
    {
        if (!enabled)
        {
            StartupEntries.DeleteTask();
            StartupEntries.RemoveRunEntries(StartupEntries.AllRunEntries);
            StandbyWatcher.Stop();
            return true;
        }
        if (AppInfo.ExePath is not { } exe)
        {
            return false;
        }
        var data = DataArgument(dataDir);
        if (StartupEntries.EnsureTask(exe, "--on-game " + data))
        {
            StartupEntries.RemoveRunEntries(StartupEntries.AllRunEntries); // nothing left at logon
            StandbyWatcher.Stop();
            return true;
        }
        return ApplyStandby(exe, data);
    }

    /// <summary>The fallback: a standby started at logon (its entry is rewritten at each start, so it follows the exe).</summary>
    private static bool ApplyStandby(string exe, string data)
    {
        if (!StartupEntries.SetStandbyEntry(exe, data))
        {
            return false;
        }
        StartupEntries.RemoveRunEntries(StartupEntries.AllRunEntries.Where(entry => entry != StartupEntries.StandbyEntry));
        try
        {
            StandbyWatcher.Start(exe, data);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The "--data" argument for a data folder, as an absolute path without a trailing slash.</summary>
    private static string DataArgument(string dataDir) => $"--data \"{Path.GetFullPath(dataDir).TrimEnd('\\', '/')}\"";

    /// <summary>
    /// "--on-game": waits for the game, then opens FCT. When none of Fortnite's start-up programs is around, the log entry
    /// came from another game with the same anti-cheat and this ends at once; it also gives up if they all go away.
    /// </summary>
    public static int RunOnGameStart(string dataDir)
    {
        if (AppInfo.ExePath is not { } exe)
        {
            return 1;
        }
        var waited = Stopwatch.StartNew();
        while (!GameProcess.IsRunning())
        {
            var stillComing = GameProcess.IsStarting() && waited.Elapsed < LauncherPatience;
            if (!stillComing)
            {
                return 0;
            }
            Thread.Sleep(1000);
        }
        if (StandbyWatcher.OptionStillOn(dataDir))
        {
            OpenFct(exe, dataDir);
        }
        return 0;
    }

    /// <summary>Opens FCT in background mode, unless it is open already or the option was turned off meanwhile.</summary>
    private static void OpenFct(string exe, string dataDir)
    {
        if (!StandbyWatcher.OptionStillOn(dataDir))
        {
            return;
        }
        if (Mutex.TryOpenExisting(AppInfo.InstanceMutexFor(dataDir), out var open))
        {
            open.Dispose();
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe, $"--auto {DataArgument(dataDir)}") { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
