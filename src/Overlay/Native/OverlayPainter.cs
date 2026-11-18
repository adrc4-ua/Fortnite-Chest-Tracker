using System.Drawing;
using System.Drawing.Drawing2D;
using static FCT.Overlay.Native.OverlayLayout;
using static FCT.Overlay.Native.Win32;

namespace FCT.Overlay.Native;

/// <summary>
/// Paints the overlay in the app's style: a black card with the gold veil of its cards, a gold stripe on the left (mint
/// during a match), "◆ COFRES" like the app's titles, the big numbers in the gold gradient, the match in a soft gold
/// chip and the stats in centred columns (treasures with the gold gem). Flat shapes and small text are plain GDI (sharp
/// ClearType); gradients, icons and the big numbers are GDI+.
/// </summary>
internal sealed class OverlayPainter
{
    private static readonly Color Gold = Color.FromArgb(232, 176, 75), GoldHigh = Color.FromArgb(248, 217, 143),
        GoldDeep = Color.FromArgb(196, 138, 44), Faint = Color.FromArgb(90, 85, 75);

    private readonly IntPtr _hdc;
    private readonly RECT _client;
    private readonly OverlayInk _ink;
    private readonly OverlayLayout _at;
    private readonly OverlayView _view;
    private readonly bool _locked, _withClose;
    private readonly OverlayButton _hover;

    // where things go (from the layout)
    private readonly int _left, _right, _headY, _numbersY, _totalY, _chipLeft, _chipTop, _statsY;
    private readonly float _blockLeft;

    private OverlayPainter(IntPtr hdc, RECT client, OverlayInk ink, OverlayLayout layout, OverlayView view, bool locked, bool withClose, OverlayButton hover)
    {
        (_hdc, _client, _ink, _at, _view, _locked, _withClose, _hover) = (hdc, client, ink, layout, view, locked, withClose, hover);
        var px = ink.Px;
        _left = px(Stripe) + px(Pad);
        _right = client.Right - px(Pad);
        _headY = px(9);
        _numbersY = _headY + layout.HeaderHeight + px(6);
        _totalY = _numbersY + (layout.NumbersHeight - layout.BigHeight - layout.CaptionHeight) / 2;
        _chipLeft = _right - layout.ChipWidth;
        _chipTop = _numbersY + (layout.NumbersHeight - layout.ChipHeight) / 2;
        _statsY = _numbersY + layout.NumbersHeight + px(8);
        // the stats block is centred between the stripe and the right edge
        _blockLeft = (px(Stripe) + client.Right - layout.StatsWidth(px)) / 2f;
    }

    /// <param name="hover">The button under a free pointer, lit in gold (the overlay never sets a cursor of its own).</param>
    public static void Paint(IntPtr hdc, RECT client, OverlayInk ink, OverlayLayout layout, OverlayView view, bool locked, bool withClose,
        OverlayButton hover = OverlayButton.None) =>
        new OverlayPainter(hdc, client, ink, layout, view, locked, withClose, hover).Paint();

    private int Px(int length) => _ink.Px(length);
    private float ColumnLeft(int i) => _blockLeft + _at.StatWidths.Take(i).Sum() + i * Px(StatGap);
    private float ColumnWidth(int i) => i < _at.StatWidths.Length ? _at.StatWidths[i] : 0;
    private float ColumnCentre(int i) => ColumnLeft(i) + ColumnWidth(i) / 2f;
    private int StatValueY => _statsY + 1 + Px(7) + _at.CaptionHeight + Px(1);

    private void Paint()
    {
        PaintFlat();
        RectangleF totalInk;
        using (var g = Graphics.FromHdc(_hdc))
        {
            totalInk = PaintSmooth(g);
        }
        PaintText(totalInk);
    }

    /// <summary>Step 1, GDI: the card, the chip and its edge, the separator and the column dividers.</summary>
    private void PaintFlat()
    {
        var client = _client;
        FillRect(_hdc, ref client, _ink.Card);
        SetBkMode(_hdc, TRANSPARENT);
        var chip = new RECT(_chipLeft, _chipTop, _right, _chipTop + _at.ChipHeight);
        OverlayInk.FillRounded(_hdc, _ink.Chip, chip, Px(12));
        OverlayInk.FrameRounded(_hdc, _ink.Edge, chip, Px(12));
        if (_view.Estimation != null && !_at.EstimationBadge.IsEmpty)
        {
            OverlayInk.FillRounded(_hdc, _ink.Chip, _at.EstimationBadge, Px(6));
            OverlayInk.FrameRounded(_hdc, _ink.Edge, _at.EstimationBadge, Px(6));
        }
        if (_view.Rows.Count == 0)
        {
            return;
        }
        var line = new RECT(_left, _statsY, _right, _statsY + 1);
        FillRect(_hdc, ref line, _ink.Line);
        // a thin gold line in the middle of each gap between columns
        var top = _statsY + 1 + Px(8);
        var bottom = _statsY + 1 + Px(7) + _at.CaptionHeight + Px(1) + _at.StatHeight - Px(1);
        for (var i = 0; i < _view.Rows.Count - 1; i++)
        {
            var x = (int)Math.Round(ColumnLeft(i) + ColumnWidth(i) + Px(StatGap) / 2f);
            var divider = new RECT(x, top, x + 1, bottom);
            FillRect(_hdc, ref divider, _ink.Edge);
        }
    }

    /// <summary>Step 2, GDI+: veil, stripe, diamond, big numbers and the gem. Returns where the total was drawn.</summary>
    private RectangleF PaintSmooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // the gold veil over the top half; the gradient runs past the area, or GDI+ restarts it on the last pixel row
        var veil = new RectangleF(0, 0, _client.Right, _client.Bottom * 0.55f);
        using (var fade = new LinearGradientBrush(new PointF(0, -1), new PointF(0, veil.Bottom + 2), Color.FromArgb(16, Gold), Color.FromArgb(0, Gold)))
        {
            g.FillRectangle(fade, veil);
        }

        // the accent stripe (the window's round region clips it): gold
        using (Brush stripe = new LinearGradientBrush(new PointF(0, -1), new PointF(0, _client.Bottom + 1), GoldHigh, Gold))
        {
            g.FillRectangle(stripe, new RectangleF(0, 0, Px(Stripe), _client.Bottom));
        }

        // ◆ before the title
        var side = Px(6);
        var saved = g.Save();
        g.TranslateTransform(_left + side * 0.75f, _headY + _at.HeaderHeight / 2f);
        g.RotateTransform(45);
        using (var gold = new SolidBrush(Gold))
        {
            g.FillRectangle(gold, -side / 2f, -side / 2f, side, side);
        }
        g.Restore(saved);

        // the total (deep gold, centred in its quadrant) and the match (lighter gold; faint when there is none)
        var totalBoxWidth = _chipLeft - _left;
        var totalInk = BigNumber(g, _view.Total, _left, _totalY, null, true, totalBoxWidth);
        var matchColor = _view.Current == null ? Faint : (Color?)null;
        BigNumber(g, _view.Current ?? "—", _chipLeft, _chipTop + Px(3), matchColor, false, _at.ChipWidth);

        for (var i = 0; i < _view.Rows.Count; i++)
        {
            var row = _view.Rows[i];
            if (row.Treasure)
            {
                var numberWidth = OverlayInk.Measure(_hdc, _ink.StatFont, StatValue(row));
                var icon = _at.StatHeight * 0.82f;
                var pair = icon + Px(3) + numberWidth;
                FCT.UI.SvgIcon.Draw(g, FCT.UI.Icons.Treasure,
                    new RectangleF(ColumnCentre(i) - pair / 2f, StatValueY + (_at.StatHeight - icon) / 2f, icon, icon), Gold, 0.3f, 1.15f);
            }
        }
        return totalInk;
    }

    /// <summary>Step 3, GDI with ClearType: title, lock and close glyphs, captions and the stats.</summary>
    private void PaintText(RectangleF totalInk)
    {
        OverlayInk.Write(_hdc, _ink.HeaderFont, OverlayInk.Champagne, Title,
            new RECT(_left + Px(12), _headY, _left + Px(12) + _at.TitleWidth, _headY + _at.HeaderHeight));

        if (_view.Estimation != null && !_at.EstimationBadge.IsEmpty)
        {
            var badgeText = L.F("EST. {0}", "EST. {0}", _view.Estimation);
            var box = _at.EstimationBadge;
            OverlayInk.Write(_hdc, _ink.LabelFont, OverlayInk.Gold, badgeText, box, DT_CENTER | DT_VCENTER);
        }

        // Segoe MDL2 Assets: E72E closed padlock, E785 open padlock, E8BB close
        const uint Glyph = DT_SINGLELINE | DT_CENTER | DT_VCENTER | DT_NOPREFIX;
        SelectObject(_hdc, _ink.IconFont);
        SetTextColor(_hdc, _hover == OverlayButton.Lock ? OverlayInk.Gold : _locked ? OverlayInk.Faint : OverlayInk.Champagne);
        var lockBox = _at.Lock;
        DrawTextW(_hdc, _locked ? "" : "", -1, ref lockBox, Glyph);
        if (_withClose)
        {
            SetTextColor(_hdc, _hover == OverlayButton.Close ? OverlayInk.Gold : OverlayInk.Champagne);
            var closeBox = _at.Close;
            DrawTextW(_hdc, "", -1, ref closeBox, Glyph);
        }

        // TOTAL centred in the quadrant between left padding and the chip
        var totalCaptionBox = new RECT(_left, _totalY + _at.BigHeight, _chipLeft, _totalY + _at.BigHeight + _at.CaptionHeight);
        OverlayInk.Write(_hdc, _ink.CaptionFont, OverlayInk.Muted, TotalCaption, totalCaptionBox, DT_CENTER);
        var captionTop = _chipTop + Px(3) + _at.BigHeight;
        OverlayInk.Write(_hdc, _ink.CaptionFont, OverlayInk.Muted, _view.CurrentCaption.ToUpperInvariant(),
            new RECT(_chipLeft, captionTop, _right, captionTop + _at.CaptionHeight), DT_CENTER);

        var labelY = _statsY + 1 + Px(7);
        for (var i = 0; i < _view.Rows.Count; i++)
        {
            var row = _view.Rows[i];
            var left = (int)Math.Round(ColumnLeft(i));
            var right = (int)Math.Round(ColumnLeft(i) + ColumnWidth(i));
            OverlayInk.Write(_hdc, _ink.LabelFont, OverlayInk.Muted, row.Label.ToUpperInvariant(), new RECT(left - Px(6), labelY, right + Px(6), labelY + _at.CaptionHeight), DT_CENTER);
            var valueY = labelY + _at.CaptionHeight + Px(1);
            if (row.Treasure)
            {
                // the number after the gem (drawn in step 2), the pair centred in the column
                var number = StatValue(row);
                var numberWidth = OverlayInk.Measure(_hdc, _ink.StatFont, number);
                var icon = (int)Math.Round(_at.StatHeight * 0.82f);
                var numberLeft = (int)Math.Round(ColumnCentre(i) - (icon + Px(3) + numberWidth) / 2f) + icon + Px(3);
                OverlayInk.Write(_hdc, _ink.StatFont, OverlayInk.Gold, number, new RECT(numberLeft, valueY, numberLeft + numberWidth + Px(2), valueY + _at.StatHeight));
            }
            else
            {
                OverlayInk.Write(_hdc, _ink.StatFont, OverlayInk.Text, row.Value, new RECT(left - Px(6), valueY, right + Px(6), valueY + _at.StatHeight), DT_CENTER);
            }
        }
    }

    /// <summary>
    /// A big number drawn as a shape, in the gold gradient (no <paramref name="solid"/>) or one colour; left-aligned at
    /// <paramref name="left"/> or centred in <paramref name="centreWidth"/>, and centred on the line of the big font.
    /// </summary>
    private RectangleF BigNumber(Graphics g, string text, float left, float top, Color? solid, bool deep, int centreWidth = 0)
    {
        var scale = _ink.Scale;
        var fontSize = 26f * scale;
        using var shape = new GraphicsPath();
        using (var family = new FontFamily("Segoe UI"))
        {
            shape.AddString(text, family, (int)FontStyle.Bold, (float)Math.Round(fontSize), PointF.Empty, StringFormat.GenericTypographic);
            if (centreWidth > 0)
            {
                var bounds = shape.GetBounds();
                var maxWidth = centreWidth - 2 * Px(2);
                if (text.Length >= 6 && bounds.Width > maxWidth)
                {
                    var reducedSize = (float)Math.Floor(fontSize * (maxWidth / bounds.Width));
                    shape.Reset();
                    shape.AddString(text, family, (int)FontStyle.Bold, Math.Max(10f * scale, reducedSize), PointF.Empty, StringFormat.GenericTypographic);
                }
            }
        }
        var ink = shape.GetBounds();
        var x = centreWidth > 0 ? left + (centreWidth - ink.Width) / 2f - ink.Left : left + Math.Max(1f, scale) - ink.Left;
        var y = top + (_at.BigHeight - ink.Height) / 2f - ink.Top;
        using (var shift = new Matrix())
        {
            shift.Translate(x, y);
            shape.Transform(shift);
        }
        var drawnBounds = shape.GetBounds();
        using Brush fill = solid is { } color
            ? new SolidBrush(color)
            : new LinearGradientBrush(new PointF(0, drawnBounds.Top - 1), new PointF(0, drawnBounds.Bottom + 1), GoldHigh, deep ? GoldDeep : Gold);
        g.FillPath(fill, shape);
        return drawnBounds;
    }
}
