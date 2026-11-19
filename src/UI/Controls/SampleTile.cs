using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// A choice card for "how many matches to measure": a big number, a short word under it (Rápida, Recomendada…) and a
/// small bar of <see cref="Levels"/> segments showing how precise it is. Selected, it is painted in the gold gradient
/// like a primary <see cref="PillButton"/>; otherwise a dark card whose edge lights up under the mouse.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SampleTile : Button
{
    private bool _hover, _pressed, _selected, _starred;
    private int _level;
    private string _caption = "";

    public SampleTile()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Panel;
        Cursor = Cursors.Hand;
    }

    /// <summary>Segments of the precision bar.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Levels { get; set; } = 4;

    /// <summary>Lit segments of the precision bar (1..<see cref="Levels"/>).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Level { get => _level; set => Rounded.Assign(this, ref _level, value); }

    /// <summary>The word under the number.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get => _caption; set => Rounded.Assign(this, ref _caption, value); }

    /// <summary>The recommended choice: a small star in the top right corner.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Starred { get => _starred; set => Rounded.Assign(this, ref _starred, value); }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected { get => _selected; set => Rounded.Assign(this, ref _selected, value); }

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

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Backdrop.Fill(this, g);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var face = Rounded.Rect(Rounded.Inner(this), 12);
        Color number, caption, barOn, barOff;
        if (_selected)
        {
            var top = _pressed ? Theme.GoldBase : _hover ? Theme.Blend(Theme.GoldHi, Color.White, 0.25f) : Theme.GoldHi;
            using var gold = new LinearGradientBrush(new PointF(0, -1), new PointF(0, Height + 1), top, _pressed ? Theme.GoldDeep : Theme.GoldBase);
            g.FillPath(gold, face);
            using var light = new Pen(Color.FromArgb(90, 255, 240, 200), 1f);
            g.DrawPath(light, face);
            number = caption = Theme.GoldInk;
            barOn = Theme.GoldInk;
            barOff = Color.FromArgb(60, Theme.GoldInk);
        }
        else
        {
            using var fill = new SolidBrush(_pressed ? Theme.Blend(Theme.PanelHover, Theme.GoldBase, 0.06f) : _hover ? Theme.PanelHover : Theme.Panel);
            g.FillPath(fill, face);
            using var edge = new Pen(_hover ? Theme.GoldLine : Theme.CardEdge, 1f);
            g.DrawPath(edge, face);
            number = Theme.Ink;
            caption = Theme.Muted;
            barOn = Theme.GoldBase;
            barOff = Theme.Line;
        }

        // number, caption and bar, centred as one block
        using var numberFont = Theme.Display(15f, FontStyle.Bold);
        using var captionFont = Theme.Text(7.75f, FontStyle.Bold);
        const TextFormatFlags Centre = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
        var numberHeight = TextRenderer.MeasureText(g, Text, numberFont, Size.Empty, Centre).Height;
        var captionHeight = TextRenderer.MeasureText(g, "Ag", captionFont, Size.Empty, Centre).Height;
        const int barHeight = 4, gapA = 0, gapB = 7;
        var block = numberHeight + gapA + captionHeight + gapB + barHeight;
        var y = Math.Max(4, (Height - block) / 2);
        TextRenderer.DrawText(g, Text, numberFont, new Rectangle(0, y, Width, numberHeight), number, Centre);
        y += numberHeight + gapA;
        TextRenderer.DrawText(g, _caption, captionFont, new Rectangle(2, y, Width - 4, captionHeight), caption, Centre | TextFormatFlags.EndEllipsis);
        y += captionHeight + gapB;

        var levels = Math.Max(1, Levels);
        const float segGap = 3f;
        var barWidth = Math.Min(Width - 24f, 52f);
        var seg = (barWidth - segGap * (levels - 1)) / levels;
        var x = (Width - barWidth) / 2f;
        for (var i = 0; i < levels; i++)
        {
            using var brush = new SolidBrush(i < _level ? barOn : barOff);
            using var pill = Rounded.Rect(new RectangleF(x, y, seg, barHeight), barHeight / 2f);
            g.FillPath(brush, pill);
            x += seg + segGap;
        }

        if (_starred)
        {
            using var starFont = Theme.Text(8f, FontStyle.Bold);
            TextRenderer.DrawText(g, "★", starFont, new Point(Width - 18, 5), _selected ? Theme.GoldInk : Theme.GoldBase, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }
}
