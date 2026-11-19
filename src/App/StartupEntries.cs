using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace FCT.App;

/// <summary>
/// Where Windows is told to start FCT: the scheduled task "Fortnite Chest Tracker" (current way, see
/// <see cref="FortniteWatch"/>) and the HKCU Run entries (fallback, and those of earlier versions to clean up).
/// FCT_TASK_NAME gives the task another name in tests, so the real one is never touched.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StartupEntries
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>Run entry of the fallback standby.</summary>
    public const string StandbyEntry = AppInfo.ShortName;

    /// <summary>Run entries FCT ever wrote ("FortniteChestTracker" was a development name, "ChestCounter" the first one).</summary>
    public static readonly string[] AllRunEntries = { StandbyEntry, "FortniteChestTracker", "ChestCounter" };

    private static string TaskName => Environment.GetEnvironmentVariable("FCT_TASK_NAME") is { Length: > 0 } test ? test : AppInfo.Name;

    /// <summary>
    /// The System-log events of Fortnite's anti-cheat driver: 7045 "service installed" and FilterManager 6 "filter
    /// loaded", for EasyAntiCheat_EOSSys (and BEDaisy, BattlEye's). Both come once per game start, ~10 s before the game.
    /// </summary>
    internal const string GameStartQuery =
        "<QueryList><Query Id=\"0\" Path=\"System\">" +
        "<Select Path=\"System\">*[System[Provider[@Name='Service Control Manager'] and EventID=7045]] and *[EventData[Data[@Name='ServiceName']='EasyAntiCheat_EOSSys' or Data[@Name='ServiceName']='BEDaisy']]</Select>" +
        "<Select Path=\"System\">*[System[Provider[@Name='Microsoft-Windows-FilterManager'] and EventID=6]] and *[EventData[Data[@Name='DeviceName']='EasyAntiCheat_EOSSys' or Data[@Name='DeviceName']='BEDaisy']]</Select>" +
        "</Query></QueryList>";

    // ---- Scheduled task ----------------------------------------------------------------------------------------------------

    public static bool TaskExists() => Schtasks($"/Query /TN \"{TaskName}\"").ExitCode == 0;

    /// <summary>Registers the task, unless it already runs <paramref name="arguments"/> of this exe on those events.</summary>
    public static bool EnsureTask(string exe, string arguments)
    {
        var (code, registered) = Schtasks($"/Query /TN \"{TaskName}\" /XML");
        var (command, actionArguments) = TaskAction(exe, arguments);
        var decoded = System.Net.WebUtility.HtmlDecode(registered);
        var upToDate = code == 0
            && decoded.Contains($"<Command>{command}</Command>", StringComparison.OrdinalIgnoreCase)
            && decoded.Contains($"<Arguments>{actionArguments}</Arguments>", StringComparison.OrdinalIgnoreCase)
            && registered.Contains("EasyAntiCheat_EOSSys", StringComparison.Ordinal);
        if (upToDate)
        {
            return true;
        }
        var definition = Path.Combine(Path.GetTempPath(), $"fct-task-{Environment.ProcessId}.xml");
        try
        {
            File.WriteAllText(definition, TaskXml(exe, arguments), System.Text.Encoding.Unicode);
            return Schtasks($"/Create /TN \"{TaskName}\" /XML \"{definition}\" /F").ExitCode == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(definition); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The exe the registered task starts (also through the guard, see <see cref="Guarded"/>); null without a task.</summary>
    public static string? TaskExe()
    {
        var (code, registered) = Schtasks($"/Query /TN \"{TaskName}\" /XML");
        if (code != 0)
        {
            return null;
        }
        var command = System.Text.RegularExpressions.Regex.Match(registered, "<Command>([^<]+)</Command>");
        var arguments = System.Text.RegularExpressions.Regex.Match(registered, "<Arguments>([^<]*)</Arguments>");
        var guarded = arguments.Success ? GuardedExe(System.Net.WebUtility.HtmlDecode(arguments.Groups[1].Value)) : null;
        return guarded ?? (command.Success ? System.Net.WebUtility.HtmlDecode(command.Groups[1].Value).Trim('"') : null);
    }

    public static void DeleteTask()
    {
        if (TaskExists())
        {
            Schtasks($"/Delete /TN \"{TaskName}\" /F");
        }
    }

    /// <summary>
    /// The task: the user's own (no admin rights), normal priority, one run at a time (the two events arrive together),
    /// also on battery, at most 10 minutes (the launcher ends long before).
    /// </summary>
    internal static string TaskXml(string exe, string arguments)
    {
        static string Esc(string text) => SecurityElement.Escape(text);
        var (command, actionArguments) = TaskAction(exe, arguments);
        var account = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        var about = L.T(
            $"Abre {AppInfo.Name} cuando arranca Fortnite («Abrir con Fortnite»). Se quita desde Configuración.",
            $"Opens {AppInfo.Name} when Fortnite starts (\"Open with Fortnite\"). Turned off in Settings.");
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>{Esc(AppInfo.Name)}</Author>
                <Description>{Esc(about)}</Description>
              </RegistrationInfo>
              <Triggers>
                <EventTrigger>
                  <Enabled>true</Enabled>
                  <Subscription>{Esc(GameStartQuery)}</Subscription>
                </EventTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{Esc(account)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>LeastPrivilege</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{Esc(command)}</Command>
                  <Arguments>{Esc(actionArguments)}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    /// <summary>
    /// What the task runs: the exe through the guard, so a task whose exe was deleted by hand deletes itself (and the
    /// update leftovers and the .NET extraction folder) the next time Fortnite starts, instead of failing forever.
    /// </summary>
    internal static (string Command, string Arguments) TaskAction(string exe, string arguments)
    {
        var cleanup = $"{Arg(SystemTool("schtasks.exe"))} /Delete /TN {Arg(TaskName)} /F >nul 2>&1 & {CommonCleanup(exe)}";
        return Guarded(exe, $"{Arg(exe)} {Args(arguments)}", cleanup) is { } guarded ? (SystemTool("conhost.exe"), guarded) : (exe, arguments);
    }

    // ---- The guard: run the exe if it is still there, clean up after it if not --------------------------------------------

    /// <summary>
    /// The arguments for conhost.exe that run <paramref name="run"/> while <paramref name="exe"/> exists and
    /// <paramref name="cleanup"/> once it is gone (deleted by hand, emptied from the Recycle Bin): what Settings →
    /// Uninstall would have removed then goes too. conhost "--headless" runs that cmd line without any window (a console
    /// program started by the task or at logon would flash one). Both parts are made of <see cref="Arg"/> tokens. Null
    /// when a path has a "%", which cmd would expand: then the exe is started directly, without the guard.
    /// </summary>
    internal static string? Guarded(string exe, string run, string cleanup)
    {
        if ((exe + run + cleanup).Contains('%'))
        {
            return null;
        }
        return $"--headless {Arg(SystemTool("cmd.exe"))} /d /c if exist {Arg(exe)} ( {run} ) else ( {cleanup} )";
    }

    /// <summary>
    /// One path or value of a guarded command line. conhost splits its command line into arguments and joins them again
    /// for cmd, quoting only those with a space: so a value with a space goes quoted (and its quotes come back), and one
    /// without goes bare, with cmd's special characters escaped ("^&amp;"), since its quotes would be lost on the way.
    /// The parentheses of the guard stand apart for the same reason.
    /// </summary>
    internal static string Arg(string value) =>
        value.Length == 0 || value.Contains(' ') ? $"\"{value}\"" : System.Text.RegularExpressions.Regex.Replace(value, "[&()<>|^]", "^$0");

    /// <summary>Arguments as FCT writes them (quoted values among bare options), with each quoted value made an <see cref="Arg"/>.</summary>
    private static string Args(string arguments) =>
        System.Text.RegularExpressions.Regex.Replace(arguments, "\"([^\"]*)\"", match => Arg(match.Groups[1].Value));

    /// <summary>The exe a guarded command line checks for; null for a command line without the guard.</summary>
    internal static string? GuardedExe(string commandLine)
    {
        var match = System.Text.RegularExpressions.Regex.Match(commandLine, "/c if exist (?:\"([^\"]+)\"|(\\S+)) \\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }
        return match.Groups[1].Success ? match.Groups[1].Value : System.Text.RegularExpressions.Regex.Replace(match.Groups[2].Value, "\\^(.)", "$1");
    }

    /// <summary>The update leftovers beside the deleted exe and its .NET extraction folder, as the uninstaller does.</summary>
    private static string CommonCleanup(string exe) =>
        $"del /f /q {Arg(exe + ".*.old")} {Arg(exe + ".old")} {Arg(exe + ".download")} >nul 2>&1 & rd /s /q {Arg(Uninstaller.ExtractionFolder(exe))} >nul 2>&1";

    private static string SystemTool(string name) => Path.Combine(Environment.SystemDirectory, name);

    /// <summary>Runs schtasks.exe hidden; (-1, "") when it cannot run or takes over 15 s.</summary>
    private static (int ExitCode, string Output) Schtasks(string arguments)
    {
        try
        {
            using var tool = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (tool == null)
            {
                return (-1, "");
            }
            var output = tool.StandardOutput.ReadToEndAsync();
            _ = tool.StandardError.ReadToEndAsync(); // drained so a full pipe can never block schtasks
            if (tool.WaitForExit(15_000))
            {
                return (tool.ExitCode, output.Result);
            }
            try { tool.Kill(); } catch (InvalidOperationException) { }
            return (-1, "");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (-1, "");
        }
    }

    // ---- Run entries (logon) --------------------------------------------------------------------------------------------------

    /// <summary>
    /// Writes the fallback standby entry (the exe with "--standby" and these arguments, through the guard: once the exe is
    /// deleted by hand, the next logon removes the entry and the rest); false when the registry refuses.
    /// </summary>
    public static bool SetStandbyEntry(string exe, string arguments)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            run.SetValue(StandbyEntry, StandbyCommand(exe, arguments));
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }

    /// <summary>The standby's Run command: "start" so the hidden cmd ends at once and the standby stays alone.</summary>
    internal static string StandbyCommand(string exe, string arguments)
    {
        var reg = SystemTool("reg.exe");
        var cleanup = $"{Arg(reg)} delete {Arg($"HKCU\\{RunKey}")} /v {Arg(StandbyEntry)} /f >nul 2>&1 & "
            + $"{Arg(reg)} delete {Arg($"HKCU\\{ApprovedKey}")} /v {Arg(StandbyEntry)} /f >nul 2>&1 & {CommonCleanup(exe)}";
        return Guarded(exe, $"start \"\" {Arg(exe)} --standby {Args(arguments)}", cleanup) is { } guarded
            ? $"\"{SystemTool("conhost.exe")}\" {guarded}"
            : $"\"{exe}\" --standby {arguments}";
    }

    /// <summary>The exes FCT's Run entries start (the guarded exe, or the quoted exe the command begins with).</summary>
    public static List<string> RunEntryExes()
    {
        var exes = new List<string>();
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            foreach (var name in AllRunEntries)
            {
                if (run?.GetValue(name) is not string command || !command.Contains("--standby", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (GuardedExe(command) is { } guarded)
                {
                    exes.Add(guarded);
                }
                else if (command.StartsWith('"') && command.IndexOf('"', 1) is var end and > 1)
                {
                    exes.Add(command[1..end]);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
        }
        return exes;
    }

    /// <summary>
    /// Removes these Run entries (and their Task Manager on/off state), but only those that start a "--standby":
    /// another program that happens to use one of the names is left alone.
    /// </summary>
    public static void RemoveRunEntries(IEnumerable<string> names)
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            foreach (var name in names)
            {
                var ours = run?.GetValue(name) is not string command || command.Contains("--standby", StringComparison.OrdinalIgnoreCase);
                if (!ours)
                {
                    continue;
                }
                run?.DeleteValue(name, throwOnMissingValue: false);
                approved?.DeleteValue(name, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            // at worst an old entry starts a standby, which sees the task and exits
        }
    }
}
