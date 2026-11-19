using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FCT.App;

/// <summary>
/// FCT.exe is built as a Windows (GUI) program so that its background launchers never flash a window. The command
/// line options still need a console: this gives the process one, reusing the terminal that started it when possible.
/// </summary>
[SupportedOSPlatform("windows")]
public static class NativeConsole
{
    /// <param name="attachToParent">True for command line options: print into the calling terminal.</param>
    public static void Ensure(bool attachToParent)
    {
        var hasConsole = GetConsoleWindow() != IntPtr.Zero;
        if (hasConsole || OutputIsRedirected())
        {
            return; // already a console, or output going to a file/pipe that Console writes to as it is
        }
        if (!(attachToParent && AttachConsole(ParentProcess)))
        {
            AllocConsole();
        }
    }

    private static bool OutputIsRedirected()
    {
        var output = GetStdHandle(-11 /* STD_OUTPUT_HANDLE */);
        if (output == IntPtr.Zero || output == new IntPtr(-1))
        {
            return false;
        }
        var kind = GetFileType(output);
        return kind == 1 /* FILE_TYPE_DISK */ || kind == 3 /* FILE_TYPE_PIPE */;
    }

    private const int ParentProcess = -1; // ATTACH_PARENT_PROCESS

    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int which);
    [DllImport("kernel32.dll")] private static extern uint GetFileType(IntPtr handle);
}
