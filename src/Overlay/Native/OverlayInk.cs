using static FCT.Overlay.Native.Win32;

namespace FCT.Overlay.Native;

/// <summary>
/// The overlay's GDI fonts and brushes. Fonts follow the screen scale (natural ClearType, nothing under 9 px: the small
/// captions were blurry at 8); brushes are the app's black and gold palette and never change.
/// </summary>
internal sealed class OverlayInk
{
    // card, thin gold edge, separator line and the soft gold chip
    public readonly IntPtr Card = CreateSolidBrush(Rgb(18, 18, 21));
    public readonly IntPtr Edge = CreateSolidBrush(Rgb(82, 65, 37));
    public readonly IntPtr Line = CreateSolidBrush(Rgb(44, 42, 38));
    public readonly IntPtr Chip = CreateSolidBrush(Rgb(39, 33, 25));

    // text colours
    public static readonly uint Text = Rgb(243, 238, 227), Muted = Rgb(150, 143, 128), Champagne = Rgb(216, 194, 143),
        Faint = Rgb(90, 85, 75), Gold = Rgb(232, 176, 75);

    public IntPtr BigFont { get; private set; }
    public IntPtr HeaderFont { get; private set; }
    public IntPtr CaptionFont { get; private set; }
    public IntPtr LabelFont { get; private set; }
    public IntPtr StatFont { get; private set; }
    public IntPtr IconFont { get; private set; }

    public float Scale { get; private set; } = 1f;

    /// <summary>A length in 96-dpi pixels at the current scale.</summary>
    public int Px(int length) => (int)Math.Round(length * Scale);

    /// <summary>Sets the scale and rebuilds the fonts for it.</summary>
    public void SetScale(float scale)
    {
        Scale = scale;
        foreach (var font in new[] { BigFont, HeaderFont, CaptionFont, LabelFont, StatFont, IconFont })
        {
            if (font != IntPtr.Zero)
            {
                DeleteObject(font);
            }
        }
        BigFont = Font(26, 800);
        HeaderFont = Font(11, 700);
        CaptionFont = Font(9, 600);
        LabelFont = Font(9, 700);
        StatFont = Font(13, 700);
        IconFont = Font(14, 400, "Segoe MDL2 Assets");
    }

    private IntPtr Font(int pixels, int weight, string face = "Segoe UI") =>
        CreateFontW(-Px(pixels), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, CLEARTYPE_NATURAL_QUALITY, 0, face);

    /// <summary>Width of a line of text (a space when empty); its height comes out too.</summary>
    public static int Measure(IntPtr hdc, IntPtr font, string text, out int height)
    {
        SelectObject(hdc, font);
        var box = new RECT();
        DrawTextW(hdc, text.Length == 0 ? " " : text, -1, ref box, DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX);
        height = box.Height;
        return box.Width;
    }

    public static int Measure(IntPtr hdc, IntPtr font, string text) => Measure(hdc, font, text, out _);

    /// <summary>A line of text in a box.</summary>
    public static void Write(IntPtr hdc, IntPtr font, uint color, string text, RECT box, uint align = DT_LEFT)
    {
        SelectObject(hdc, font);
        SetTextColor(hdc, color);
        DrawTextW(hdc, text, -1, ref box, DT_SINGLELINE | DT_NOPREFIX | align);
    }

    /// <summary>A rounded rectangle filled with a brush (edges included).</summary>
    public static void FillRounded(IntPtr hdc, IntPtr brush, RECT box, int radius)
    {
        var region = CreateRoundRectRgn(box.Left, box.Top, box.Right + 1, box.Bottom + 1, radius, radius);
        FillRgn(hdc, region, brush);
        DeleteObject(region);
    }

    /// <summary>A one-pixel outline of a rounded rectangle.</summary>
    public static void FrameRounded(IntPtr hdc, IntPtr brush, RECT box, int radius)
    {
        var region = CreateRoundRectRgn(box.Left, box.Top, box.Right + 1, box.Bottom + 1, radius, radius);
        FrameRgn(hdc, region, brush, 1, 1);
        DeleteObject(region);
    }
}
