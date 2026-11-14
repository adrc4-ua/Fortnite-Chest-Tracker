using System.Diagnostics;
using System.Runtime.Versioning;

namespace FCT.App;

/// <summary>
/// Uninstall (Settings → Uninstall, or FCT.exe --uninstall): removes every trace FCT leaves on Windows, whatever the
/// settings: the scheduled task of "Open with Fortnite", the entries at Windows logon of earlier versions (and the
/// standby process they start), the leftovers of updates and the exe itself. The player's data (%APPDATA%\FCT) is
/// kept: it is only deleted by hand, if the player wants.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Uninstaller
{
    /// <summary>Removes the task, the startup entries and the standby. Safe to call more than once.</summary>
    public static void RemoveTraces()
    {
        FortniteWatch.Apply(false, Store.DefaultFolder()); // task + entries at logon + standby, whatever the option says
        Updater.CleanupOld();
    }

    /// <summary>
    /// Deletes the exe (and its update leftovers and, with <paramref name="shared"/>, its .NET extraction folder, which
    /// any other copy named alike shares) once this process has ended: a hidden cmd waits a moment and deletes them. The
    /// folder that held the exe is left as it was.
    /// </summary>
    public static void DeleteSelfAfterExit(bool shared = true)
    {
        if (AppInfo.ExePath is not { } exe)
        {
            return;
        }
        var commands = new List<string> { "ping -n 4 127.0.0.1 >nul", $"del /f /q \"{exe}\"", $"del /f /q \"{exe}.*.old\" \"{exe}.old\" \"{exe}.download\"" };
        var extracted = Path.Combine(Path.GetTempPath(), ".net", Path.GetFileNameWithoutExtension(exe));
        if (shared && Directory.Exists(extracted))
        {
            commands.Add($"rd /s /q \"{extracted}\"");
        }
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /c " + string.Join(" & ", commands))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(), // not the exe's folder: it must not stay in use
            })?.Dispose();
        }
        catch
        {
            // nothing else to do: the player can delete the file by hand
        }
    }

    /// <summary>FCT.exe --uninstall (from a terminal).</summary>
    public static int RunFromCommandLine()
    {
        if (Mutex.TryOpenExisting(AppInfo.InstanceMutexFor(Store.DefaultFolder()), out var open))
        {
            open.Dispose();
            Console.WriteLine(L.F("{0} está abierto: ciérralo (o usa Configuración → Desinstalar) y vuelve a probar.",
                "{0} is open: close it (or use Settings → Uninstall) and try again.", AppInfo.ShortName));
            return 1;
        }
        RemoveTraces();
        DeleteSelfAfterExit();
        Console.WriteLine(L.F("{0} desinstalado: sin tarea programada ni entradas de inicio, y el exe se borra en unos segundos. Tus datos siguen en {1} (bórralos a mano si quieres).",
            "{0} uninstalled: no scheduled task or startup entries, and the exe is deleted in a few seconds. Your data is still in {1} (delete it by hand if you want).",
            AppInfo.ShortName, Store.DefaultFolder()));
        return 0;
    }
}
