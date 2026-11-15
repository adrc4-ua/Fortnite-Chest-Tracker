using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FCT.PostMatch;

/// <summary>Hidden command line tools to test the post-match screen reading (not in the help).</summary>
internal static class PostMatchTools
{
    /// <summary>--read-treasure-screen &lt;image&gt;: the cards found in a screenshot and the gizmo of each.</summary>
    public static int ReadImage(string path)
    {
        using var bitmap = new Bitmap(path);
        var frame = FromBitmap(bitmap);
        Print(frame);
        return 0;
    }

    /// <summary>
    /// --capture-window &lt;exact window title&gt; [out.png]: captures that window the same way as after a match and
    /// reads it. Only this test writes the frame to a file, and only when a file is given.
    /// </summary>
    public static int CaptureWindow(string title, string? output)
    {
        var hwnd = FindWindowByTitle(title);
        if (hwnd == IntPtr.Zero)
        {
            Console.WriteLine($"  No window titled \"{title}\".");
            return 1;
        }
        var watch = Stopwatch.StartNew();
        var frame = WindowCapture.CaptureAsync(hwnd, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        if (frame == null)
        {
            Console.WriteLine("  Capture failed (minimized window, or capture not supported).");
            return 1;
        }
        Console.WriteLine($"  Captured {frame.Width}×{frame.Height} in {watch.ElapsedMilliseconds} ms.");
        if (output != null)
        {
            using var bitmap = ToBitmap(frame);
            bitmap.Save(output, ImageFormat.Png);
            Console.WriteLine($"  Saved to {output}.");
        }
        Print(frame);
        return 0;
    }

    public static IntPtr FindWindowByTitle(string title)
    {
        var found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            var text = new System.Text.StringBuilder(512);
            GetWindowText(h, text, text.Capacity);
            if (IsWindowVisible(h) && text.ToString() == title)
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);

    /// <summary>
    /// The cards found, with how close the closest gizmo is (and the next one), and each card's print: a new reference
    /// for treasure-prints.json is that print, from a capture where the card shows the gizmo clearly.
    /// </summary>
    private static void Print(WindowCapture.Frame frame)
    {
        var cards = TreasureScreenReader.Read(frame);
        Console.WriteLine($"  {cards.Count} treasure card(s)");
        foreach (var c in cards)
        {
            var (closest, best, next) = TreasureFingerprint.Match(frame, c.X, c.Y, c.Width, c.Height);
            Console.WriteLine($"    {c.Item}  ({c.X},{c.Y} {c.Width}×{c.Height})  closest {closest ?? "-"} {best:0.000}, next {next:0.000}");
            Console.WriteLine($"      print {TreasureFingerprint.Encode(TreasureFingerprint.Of(frame, c.X, c.Y, c.Width, c.Height))}");
        }
    }

    private static WindowCapture.Frame FromBitmap(Bitmap source)
    {
        using var bitmap = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[bitmap.Width * bitmap.Height * 4];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * bitmap.Width * 4, bitmap.Width * 4);
            }
            return new WindowCapture.Frame(bitmap.Width, bitmap.Height, bytes);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static Bitmap ToBitmap(WindowCapture.Frame frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        for (var y = 0; y < frame.Height; y++)
        {
            Marshal.Copy(frame.Bgra, y * frame.Width * 4, data.Scan0 + y * data.Stride, frame.Width * 4);
        }
        bitmap.UnlockBits(data);
        return bitmap;
    }
}
