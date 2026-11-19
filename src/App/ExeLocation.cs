using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace FCT.App;

/// <summary>
/// Where this process's exe is now. Windows never lets a running exe be deleted, but it does let it be moved: sent to the
/// Recycle Bin, FCT keeps running from there. A handle kept open on the exe (sharing everything, so it never gets in the
/// way of a move, a rename or an update) follows the file wherever it goes; once it lands in the bin, FCT uninstalls
/// itself as Settings → Uninstall does (<see cref="InRecycleBin"/>).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ExeLocation
{
    private static readonly object Gate = new();
    private static SafeFileHandle? _handle;

    /// <summary>Opens the handle that follows the exe; call it first thing, before the exe can be moved. Safe to repeat.</summary>
    public static void Track()
    {
        lock (Gate)
        {
            if (_handle != null || AppInfo.ExePath is not { } exe)
            {
                return;
            }
            try
            {
                _handle = File.OpenHandle(exe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // unknown from here on: the exe is taken to stay where it started
            }
        }
    }

    /// <summary>The exe's full path now (after any move); where it started when that cannot be told; null without an exe.</summary>
    public static string? Current
    {
        get
        {
            lock (Gate)
            {
                if (_handle is { IsInvalid: false, IsClosed: false } handle && FinalPath(handle) is { } path)
                {
                    return path;
                }
            }
            return AppInfo.ExePath;
        }
    }

    /// <summary>The exe has been sent to the Recycle Bin (while this process runs from it).</summary>
    public static bool InRecycleBin => Current is { } path && IsInRecycleBin(path);

    internal static bool IsInRecycleBin(string path) => path.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The bin keeps each file as "$R…" with its record "$I…" beside it (name and where it came from): deleted together,
    /// so the bin does not go on listing a file that is gone. Null outside the bin.
    /// </summary>
    internal static string? RecycleBinRecord(string path)
    {
        var name = Path.GetFileName(path);
        if (!IsInRecycleBin(path) || !name.StartsWith("$R", StringComparison.OrdinalIgnoreCase) || Path.GetDirectoryName(path) is not { } folder)
        {
            return null;
        }
        return Path.Combine(folder, "$I" + name[2..]);
    }

    private static string? FinalPath(SafeFileHandle handle)
    {
        var buffer = new char[1024];
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length == 0 || length >= buffer.Length)
        {
            return null;
        }
        var path = new string(buffer, 0, (int)length);
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[8..];
        }
        return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, [Out] char[] path, uint size, uint flags);
}
