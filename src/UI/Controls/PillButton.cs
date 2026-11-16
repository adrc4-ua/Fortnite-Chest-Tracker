using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// A rounded button. With <see cref="Theme.Accent"/> as its colour it is the primary one (gold gradient, dark text);
/// otherwise a dark card with a thin gold edge that lights up under the mouse. Optional extras: an icon, left alignment
/// (like the page's menu), a status dot, a small tag on the right (<see cref="Chip"/>) and a dropdown arrow.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PillButton : Button
{
    private const TextFormatFlags OneLine = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
    private bool _hover, _pressed;
    private string? _chip;
    private bool _chipOn;
    private Color? _indicator;

    public PillButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Panel;
        ForeColor = Theme.Ink;
        Font = Theme.Text(9f, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    protected override void WndProc(ref Message m)
    {
        const int WmEraseBkgnd = 0x0014;
        if (m.Msg == WmEraseBkgnd)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Background under the mouse.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Theme.PanelHover;

    /// <summary>Corner radius in pixels.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 12;

    /// <summary>An SVG icon (one of <see cref="Icons"/>) before the text, or null.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? IconSvg { get; set; }

    /// <summary>Icon and text on the left, like the entries of the page's menu.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool LeftAligned { get; set; }

    /// <summary>A gold arrow on the right; the text stays centred on the whole button, not next to the arrow.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Dropdown { get; set; }

    /// <summary>A small tag on the right (such as "Activado"), or null.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Chip { get => _chip; set => Rounded.Assign(this, ref _chip, value); }

    /// <summary>The tag in gold (on) or dimmed (off).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ChipOn { get => _chipOn; set => Rounded.Assign(this, ref _chipOn, value); }

    /// <summary>A status dot before the text, with an edge of the same colour; null for none.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? Indicator { get => _indicator; set => Rounded.Assign(this, ref _indicator, value); }

    private bool Primary => BackColor.ToArgb() == Theme.Accent.ToArgb();

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    // the whole rounded face is painted in OnPaint
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Backdrop.Fill(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var (textColor, iconColor, iconFill) = PaintFace(g);
        var iconSize = IconSvg != null ? Math.Min(Height * 0.46f, Font.Height * 1.25f) : 0f;
        var dotSize = _indicator != null ? Math.Max(6f, Font.Height * 0.45f) : 0f;
        var gap = Font.Height * 0.5f;
        var lead = iconSize > 0 ? iconSize + gap : dotSize > 0 ? dotSize + gap * 0.8f : 0f;
        var chipWidth = PaintChip(g);
        var arrowRoom = PaintArrow(g);

        var textWidth = TextRenderer.MeasureText(g, Text, Font, new Size(int.MaxValue, int.MaxValue), OneLine).Width;
        var left = LeftAligned
            ? Math.Max(10f, Height * 0.32f)
            : Math.Max(4f + arrowRoom, (Width - chipWidth - (lead + textWidth)) / 2f);
        if (iconSize > 0)
        {
            SvgIcon.Draw(g, IconSvg!, new RectangleF(left, (Height - iconSize) / 2f, iconSize, iconSize), iconColor, iconFill);
        }
        else if (_indicator is { } dot)
        {
            using var brush = new SolidBrush(dot);
            g.FillEllipse(brush, left, (Height - dotSize) / 2f, dotSize, dotSize);
        }

        // centred text gets a box just its size: with the whole width, long text was cut with "…" too early
        var room = Math.Max(10f, Width - chipWidth - left - lead - 2f - arrowRoom);
        var box = Rectangle.Round(new RectangleF(left + lead, 0, LeftAligned ? room : Math.Min(room, textWidth + 4f), Height));
        TextRenderer.DrawText(g, Text, Font, box, textColor, OneLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
    }

    /// <summary>The rounded face and its edge; returns the colours of text and icon, and the icon's fill strength.</summary>
    private (Color Text, Color Icon, float IconFill) PaintFace(Graphics g)
    {
        using var face = Rounded.Rect(Rounded.Inner(this), Radius);
        if (Primary)
        {
            // the gold gradient of the page's section icons, with a light edge
            var top = _pressed ? Theme.GoldBase : _hover ? Theme.Blend(Theme.GoldHi, Color.White, 0.25f) : Theme.GoldHi;
            using var gold = new LinearGradientBrush(new PointF(0, -1), new PointF(0, Height + 1), top, _pressed ? Theme.GoldDeep : Theme.GoldBase);
            g.FillPath(gold, face);
            using var light = new Pen(Color.FromArgb(90, 255, 240, 200), 1f);
            g.DrawPath(light, face);
            return (Theme.GoldInk, Theme.GoldInk, 0.35f);
        }

        var back = _pressed ? Theme.Blend(HoverColor, Theme.GoldBase, 0.06f) : _hover ? HoverColor : BackColor;
        using var fill = new SolidBrush(back);
        g.FillPath(fill, face);
        var edgeColor = _indicator is { } dot ? Color.FromArgb(150, dot) : _hover ? Theme.GoldLine : Theme.CardEdge;
        using var edge = new Pen(edgeColor, 1f);
        g.DrawPath(edge, face);
        return (Enabled ? ForeColor : Theme.Faint, Enabled ? Theme.GoldBase : Theme.Faint, 0.22f);
    }

    /// <summary>The tag on the right; returns the width it takes, margin included (0 without a tag).</summary>
    private float PaintChip(Graphics g)
    {
        if (_chip is not { Length: > 0 } chip)
        {
            return 0f;
        }
        using var font = new Font(Font.FontFamily, Font.Size * 0.82f, FontStyle.Bold);
        var textWidth = TextRenderer.MeasureText(g, chip, font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
        var height = Math.Min(Height - 10f, font.Height + 6f);
        var width = textWidth + height;
        var margin = Math.Max(8f, Height * 0.3f);
        var box = new RectangleF(Width - margin - width, (Height - height) / 2f, width, height);
        using var shape = Rounded.Rect(box, height / 2f);
        using var fill = new SolidBrush(_chipOn ? Theme.GoldSoft : Theme.Bg2);
        using var edge = new Pen(_chipOn ? Theme.GoldLine : Theme.Line, 1f);
        g.FillPath(fill, shape);
        g.DrawPath(edge, shape);
        TextRenderer.DrawText(g, chip, font, Rectangle.Round(box), _chipOn ? Theme.GoldHi : Theme.Muted,
            TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return width + margin;
    }

    /// <summary>The dropdown arrow; returns the room to keep free on both sides so the text stays centred (0 without it).</summary>
    private float PaintArrow(Graphics g)
    {
        if (!Dropdown)
        {
            return 0f;
        }
        var size = Math.Max(7f, Font.Height * 0.5f);
        var margin = Math.Max(9f, Height * 0.38f);
        var x = Width - margin - size;
        var middle = Height / 2f;
        using var pen = new Pen(Enabled ? Theme.GoldBase : Theme.Faint, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[] { new PointF(x, middle - size * 0.25f), new PointF(x + size / 2f, middle + size * 0.28f), new PointF(x + size, middle - size * 0.25f) });
        return size + margin + 4f;
    }
}
