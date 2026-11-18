using static FCT.Overlay.Native.Win32;

namespace FCT.Overlay.Native;

/// <summary>
/// Sizes and places of the overlay for one view, measured with the real fonts:
/// <code>
///  ◆ CHESTS          [● LIVE] 🔒 ✕
///  1.234             ┌───────┐
///  TOTAL             │  12   │
///                    │ MATCH │
///                    └───────┘
///  ───────────────────────────────
///   TODAY  │  AVERAGE  │  ★
///    48    │   9.6     │  3
/// </code>
/// </summary>
internal sealed class OverlayLayout
{
    // in 96-dpi pixels
    public const int MinWidth = 170, Pad = 10, Stripe = 3, ColumnGap = 16, StatGap = 24;
    public const string TotalCaption = "TOTAL";

    public int HeaderHeight, BigHeight, CaptionHeight, StatHeight;
    public int TotalWidth, ChipWidth, ChipHeight, NumbersHeight;
    public int TitleWidth;
    public int[] StatWidths = Array.Empty<int>();
    public int Width, Height;
    public RECT Lock, Close;
    public RECT EstimationBadge;

    /// <summary>The button at a point of the overlay (relative to it).</summary>
    public OverlayButton ButtonAt(int x, int y, bool withClose) =>
        Lock.Contains(x, y) ? OverlayButton.Lock : withClose && Close.Contains(x, y) ? OverlayButton.Close : OverlayButton.None;

    public static string Title => L.T("COFRES", "CHESTS");

    /// <summary>A stat's value as drawn: treasures without their emoji (the gem is drawn instead).</summary>
    public static string StatValue(OverlayRow row) => row.Treasure ? row.Value.Replace(OverlayModel.TreasureIcon, "").Trim() : row.Value;

    /// <summary>Measures <paramref name="view"/>.</summary>
    public static OverlayLayout Measure(IntPtr hdc, OverlayInk ink, OverlayView view, bool withClose)
    {
        var px = ink.Px;
        var at = new OverlayLayout();

        var titleWidth = OverlayInk.Measure(hdc, ink.HeaderFont, Title, out at.HeaderHeight);
        at.TitleWidth = titleWidth;

        // the big total on the left; the last match in a chip on the right (capped at 5 digits since 6+ scales down to fit the quadrant)
        var totalMeasureText = view.Total.Count(char.IsDigit) > 5 ? "99.999" : view.Total;
        at.TotalWidth = Math.Max(OverlayInk.Measure(hdc, ink.BigFont, totalMeasureText, out at.BigHeight),
            OverlayInk.Measure(hdc, ink.CaptionFont, TotalCaption, out at.CaptionHeight));
        var matchWidth = Math.Max(OverlayInk.Measure(hdc, ink.BigFont, view.Current ?? "—"),
            OverlayInk.Measure(hdc, ink.CaptionFont, OverlayModel.LastMatchCaption.ToUpperInvariant()));
        at.ChipWidth = matchWidth + 2 * px(12);
        at.ChipHeight = at.BigHeight + at.CaptionHeight + px(8);
        at.NumbersHeight = Math.Max(at.BigHeight + at.CaptionHeight, at.ChipHeight);
        var numbersWidth = at.TotalWidth + at.ChipWidth;

        // the stats strip: a column per row, each as wide as its label or value
        at.StatWidths = new int[view.Rows.Count];
        for (var i = 0; i < view.Rows.Count; i++)
        {
            var row = view.Rows[i];
            var valueWidth = OverlayInk.Measure(hdc, ink.StatFont, StatValue(row), out var valueHeight);
            if (row.Treasure)
            {
                valueWidth += valueHeight + px(3); // the gem before the number
            }
            at.StatWidths[i] = Math.Max(OverlayInk.Measure(hdc, ink.LabelFont, row.Label.ToUpperInvariant()), valueWidth);
            at.StatHeight = Math.Max(at.StatHeight, valueHeight);
        }

        var contentWidth = Math.Max(numbersWidth, at.StatsWidth(px) + px(6));
        at.Width = Math.Max(px(MinWidth), contentWidth + 2 * px(Pad) + px(Stripe));

        // buttons in the top right corner: [lock] [close]; without close the lock takes the corner
        var corner = new RECT(at.Width - px(20), px(4), at.Width - px(4), px(9) + at.HeaderHeight + px(4));
        if (withClose)
        {
            at.Close = corner;
            at.Lock = new RECT(corner.Left - px(18), corner.Top, corner.Left - px(2), corner.Bottom);
        }
        else
        {
            at.Lock = corner;
        }

        if (view.Estimation != null)
        {
            var badgeText = L.F("EST. {0}", "EST. {0}", view.Estimation);
            var textWidth = OverlayInk.Measure(hdc, ink.LabelFont, badgeText, out var textHeight);
            var badgeWidth = textWidth + 2 * px(4);
            var badgeHeight = textHeight + px(2);
            at.HeaderHeight = Math.Max(at.HeaderHeight, badgeHeight);
            var badgeTop = px(9) + Math.Max(0, (at.HeaderHeight - badgeHeight) / 2);
            var titleRight = px(Stripe) + px(Pad) + px(12) + titleWidth;
            var badgeLeft = titleRight + px(8);
            if (badgeLeft + badgeWidth > at.Lock.Left - px(3))
            {
                badgeLeft = at.Lock.Left - px(3) - badgeWidth;
            }
            at.EstimationBadge = new RECT(badgeLeft, badgeTop, badgeLeft + badgeWidth, badgeTop + badgeHeight);
        }

        at.Height = px(9) + at.HeaderHeight + px(6) + at.NumbersHeight + px(8);
        if (view.Rows.Count > 0)
        {
            at.Height += 1 + px(7) + at.CaptionHeight + px(1) + at.StatHeight + px(9);
        }

        // Update corner bottom with final at.HeaderHeight
        at.Lock.Bottom = at.Close.Bottom = px(9) + at.HeaderHeight + px(4);
        return at;
    }

    /// <summary>The stats as one block: the columns plus the same gap between each two.</summary>
    public int StatsWidth(Func<int, int> px) => StatWidths.Sum() + Math.Max(0, StatWidths.Length - 1) * px(StatGap);
}

/// <summary>The overlay's buttons.</summary>
internal enum OverlayButton { None, Lock, Close }
