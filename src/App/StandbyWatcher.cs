using System.Diagnostics;
using System.Runtime.Versioning;

namespace FCT.App;

/// <summary>
/// The fallback for "open with Fortnite" where the scheduled task cannot be created: a tiny invisible process started
/// at logon ("--standby") that looks for the game now and then, opens FCT when it appears and, so a player who closed
/// FCT on purpose is not bothered again, waits for that game session to end before looking again.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StandbyWatcher
{
    private static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(5), CheckGameGoneEvery = TimeSpan.FromSeconds(10);

    /// <summary>Starts the standby, unless one is already waiting.</summary>
    public static void Start(string exe, string dataArgument)
    {
        if (Mutex.TryOpenExisting(AppInfo.StandbyMutex, out var waiting))
        {
            waiting.Dispose();
            return;
        }
        Process.Start(new ProcessStartInfo(exe, $"--standby {dataArgument}") { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
    }

    /// <summary>Asks a running standby to end.</summary>
    public static void Stop()
    {
        if (EventWaitHandle.TryOpenExisting(AppInfo.StandbyStopEvent, out var stop))
        {
            using (stop)
            {
                stop.Set();
            }
        }
    }

    /// <summary>
    /// The standby itself. Ends when asked to, when the option is turned off, or right away when another standby runs or
    /// the scheduled task exists (then it was an entry left by an earlier version, removed here).
    /// </summary>
    /// <param name="openFct">Opens FCT when the game is up.</param>
    public static int Run(string dataDir, Action<string, string> openFct)
    {
        using var onlyOne = new Mutex(false, AppInfo.StandbyMutex);
        if (!Claim(onlyOne))
        {
            return 0;
        }
        if (StartupEntries.TaskExists())
        {
            StartupEntries.RemoveRunEntries(StartupEntries.AllRunEntries);
            return 0;
        }
        using var stopRequest = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.StandbyStopEvent);
        BeQuiet();
        if (AppInfo.ExePath is not { } exe)
        {
            return 1;
        }

        bool StopAsked(TimeSpan wait) => stopRequest.WaitOne(wait);
        while (!StopAsked(LookEvery))
        {
            if (ExeLocation.InRecycleBin)
            {
                // thrown away by hand: as Settings → Uninstall (the data stays)
                Uninstaller.RemoveTraces();
                Uninstaller.DeleteSelfAfterExit();
                return 0;
            }
            if (!GameProcess.IsRunning())
            {
                continue;
            }
            if (!OptionStillOn(dataDir))
            {
                return 0;
            }
            openFct(exe, dataDir);
            // the same game session: never open FCT twice
            while (GameProcess.IsRunning() && !ExeLocation.InRecycleBin)
            {
                if (StopAsked(CheckGameGoneEvery))
                {
                    return 0;
                }
            }
            GC.Collect();
        }
        return 0;
    }

    /// <summary>Takes the one-standby lock; a standby that was killed leaves it abandoned, and this one takes over.</summary>
    private static bool Claim(Mutex onlyOne)
    {
        try
        {
            return onlyOne.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    /// <summary>Idle priority, Windows' efficiency mode and a small heap: it should cost nothing while it waits.</summary>
    private static void BeQuiet()
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            self.PriorityClass = ProcessPriorityClass.Idle;
            EfficiencyMode.Enable();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
        GC.Collect();
    }

    public static bool OptionStillOn(string dataDir) => new Store(dataDir).LoadConfig() is not { StartWithFortnite: false };
}
