using static FCT.Overlay.Native.Win32;

namespace FCT.Overlay.Native;

/// <summary>
/// Whether a window of the game is open and in sight on some screen, and which process it belongs to (a new process is
/// a new launch of Fortnite). In sight: not minimized, and not mostly covered by the desktop or by other windows above it
/// (in windowed fullscreen Windows never reports the game as minimized: it only stops being in front).
/// </summary>
internal sealed class GameWindows
{
    private readonly Func<int, bool>? _isGame;
    private readonly Dictionary<uint, bool> _known = new(); // process id → is it the game

    /// <param name="isGame">Given a process id, whether it is the game; null means "always open" (previews).</param>
    public GameWindows(Func<int, bool>? isGame) => _isGame = isGame;

    public bool Watching => _isGame != null;

    /// <summary>Process of the game window found by the last <see cref="Open"/> (0 for none).</summary>
    public uint Process { get; private set; }

    /// <summary>The process seen when the overlay last came on screen for a launch.</summary>
    public uint LastLaunch { get; set; }

    public bool Open()
    {
        Process = 0;
        if (_isGame == null)
        {
            return true;
        }
        if (_known.Count > 512)
        {
            _known.Clear();
        }
        var own = (uint)Environment.ProcessId;
        var found = false;
        var gameWindow = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window) || GetWindowThreadProcessId(window, out var pid) == 0 || pid == own)
            {
                return true;
            }
            if (!_known.TryGetValue(pid, out var isGame))
            {
                _known[pid] = isGame = _isGame((int)pid);
            }
            found = isGame && OnScreen(window);
            if (found)
            {
                Process = pid;
                gameWindow = window;
            }
            return !found;
        }, IntPtr.Zero);
        return found && !Covered(gameWindow, Process, own);
    }

    /// <summary>How much of the game's window must be covered for the game to count as out of sight.</summary>
    private const int CoveredPercent = 80;

    /// <summary>Points looked at over the game's window to measure how much of it is covered (columns × rows).</summary>
    private const int Columns = 32, Rows = 18;

    /// <summary>
    /// The game is out of sight: the desktop is in front (Win + D, even while a desktop menu or Start holds the focus
    /// over it), or the windows above it, whoever has the focus and
    /// whichever screen they were opened from, cover most of its window on its screen (all of them together). Measured
    /// on the game's own window, so it holds in windowed fullscreen and in a plain window alike (a minimized game is
    /// already out: Windows reports it).
    /// </summary>
    private static bool Covered(IntPtr game, uint gameProcess, uint own)
    {
        if (DesktopInFront())
        {
            return true;
        }
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetWindowRect(game, out var window) || !GetMonitorInfoW(MonitorFromWindow(game, MONITOR_DEFAULTTONEAREST), ref info))
        {
            return false;
        }
        // the part of the game's window on its screen (in a window it may stick out)
        var screen = info.rcMonitor;
        var area = new RECT(Math.Max(window.Left, screen.Left), Math.Max(window.Top, screen.Top), Math.Min(window.Right, screen.Right), Math.Min(window.Bottom, screen.Bottom));
        if (area.Width <= 0 || area.Height <= 0)
        {
            return false;
        }

        // top-level windows come front to back: those before the game's are above it
        var above = new List<RECT>();
        var reached = false;
        var desktopAbove = false;
        EnumWindows((other, _) =>
        {
            if (other == game)
            {
                reached = true;
                return false;
            }
            if (IsWindowVisible(other) && ClassOf(other) is "Progman" or "WorkerW")
            {
                // the desktop raised over the game (Win + D): out of sight, whoever has the focus (a desktop menu, Start…)
                desktopAbove = true;
                return false;
            }
            if (Hides(other, gameProcess, own, out var box) && box.Right > area.Left && box.Left < area.Right && box.Bottom > area.Top && box.Top < area.Bottom)
            {
                above.Add(box);
            }
            return true;
        }, IntPtr.Zero);
        if (desktopAbove)
        {
            return true;
        }
        if (!reached || above.Count == 0)
        {
            return false;
        }

        var visible = 0;
        for (var row = 0; row < Rows; row++)
        {
            var y = area.Top + (2 * row + 1) * area.Height / (2 * Rows);
            for (var column = 0; column < Columns; column++)
            {
                var x = area.Left + (2 * column + 1) * area.Width / (2 * Columns);
                if (!above.Exists(box => box.Contains(x, y)))
                {
                    visible++;
                }
            }
        }
        return visible * 100 <= Columns * Rows * (100 - CoveredPercent);
    }

    /// <summary>
    /// The desktop has the focus (Win + D): it spans every screen, so the game is out of sight wherever it is. Only the
    /// quick path: with the focus elsewhere (a desktop menu, Start…) the raised desktop is caught by its place in the
    /// z-order instead.
    /// </summary>
    private static bool DesktopInFront() => ClassOf(GetForegroundWindow()) is "Progman" or "WorkerW";

    /// <summary>
    /// Screen capture tools: while choosing what to capture they lay a window over the whole screen, and the overlay must
    /// stay (it is part of the picture). Process names, without ".exe".
    /// </summary>
    private static readonly HashSet<string> CaptureTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "SnippingTool", "ScreenClippingHost", "ScreenSketch", "ShareX", "Lightshot", "Greenshot", "flameshot", "PicPick",
        "Snagit32", "SnagitCapture", "SnagitEditor", "Gyazo", "GyazoReplay", "Monosnap", "FastStone Capture", "FSCapture",
        "obs64", "GameBar", "GameBarFTServer", "XboxGameBarWidgets", "NVIDIA Share", "NVIDIA Overlay", "RadeonSoftware", "AMDRSServ",
    };

    private static readonly Dictionary<uint, bool> CaptureProcesses = new();

    /// <summary>
    /// A window above the game that hides what is under it, with its place. Not: hidden, minimized or cloaked windows,
    /// FCT's and the game's own, windows the mouse goes through or nearly invisible ones (overlays like a graphics
    /// driver's, spanning the whole screen), "always on top" ones (screen captures choosing their area, game bars: the
    /// game is still seen through or around them), screen capture tools, and the shell's: taskbar, its menus and panels,
    /// Start, and the Alt + Tab / Win + Tab switchers.
    /// </summary>
    private static bool Hides(IntPtr window, uint gameProcess, uint own, out RECT box)
    {
        box = default;
        if (!IsWindowVisible(window) || IsIconic(window) || GetWindowThreadProcessId(window, out var pid) == 0 || pid == own || pid == gameProcess)
        {
            return false;
        }
        if (DwmGetWindowAttribute(window, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }
        var style = (long)GetWindowLongPtrW(window, GWL_EXSTYLE);
        if ((style & (WS_EX_TRANSPARENT | WS_EX_TOPMOST)) != 0 || IsCaptureTool(pid))
        {
            return false;
        }
        if ((style & WS_EX_LAYERED) != 0 && GetLayeredWindowAttributes(window, out _, out var alpha, out var how) && (how & LWA_ALPHA) != 0 && alpha < 64)
        {
            return false;
        }
        switch (ClassOf(window))
        {
            case "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland"
                or "Xaml_WindowedPopupClass" or "#32768" or "Windows.UI.Core.CoreWindow" or "XamlExplorerHostIslandWindow"
                or "MultitaskingViewFrame" or "TaskSwitcherWnd" or "ForegroundStaging":
                return false;
        }
        return GetWindowRect(window, out box) && box.Width > 0 && box.Height > 0;
    }

    private static bool IsCaptureTool(uint pid)
    {
        if (CaptureProcesses.Count > 512)
        {
            CaptureProcesses.Clear();
        }
        if (!CaptureProcesses.TryGetValue(pid, out var capture))
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                capture = CaptureTools.Contains(process.ProcessName);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                capture = false; // gone
            }
            CaptureProcesses[pid] = capture;
        }
        return capture;
    }

    private static string ClassOf(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return "";
        }
        var name = new System.Text.StringBuilder(64);
        GetClassNameW(window, name, name.Capacity);
        return name.ToString();
    }

    /// <summary>
    /// Really on screen: not a tiny or helper window, inside some monitor (Windows parks minimized windows far away)
    /// and not hidden by Windows (cloaked).
    /// </summary>
    private static bool OnScreen(IntPtr window) =>
        GetWindowRect(window, out var box) && box.Width >= 200 && box.Height >= 150
        && MonitorFromWindow(window, MONITOR_DEFAULTTONULL) != IntPtr.Zero
        && !(DwmGetWindowAttribute(window, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0);
}
