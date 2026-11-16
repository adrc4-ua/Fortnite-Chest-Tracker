using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// An icon tile of the page (the figures' and the treasures'): see-through gold with a gold edge and a light gold icon.
/// <see cref="Solid"/> is the solid gold tile of the section heads, with a dark icon.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IconTile : Control
{
    public IconTile()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? IconSvg { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Solid { get; set; }

    protected override void OnPaintBackground(PaintEventArgs e) => Backdrop.Fill(this, e.Graphics);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = Rounded.Inner(this);
        using var tile = Rounded.Rect(box, Width * 0.3f);
        if (Solid)
        {
            using var gold = Theme.GoldBrush(box);
            g.FillPath(gold, tile);
        }
        else
        {
            var under = Parent?.BackColor ?? Theme.Card;
            using var veil = new LinearGradientBrush(new PointF(0, -1), new PointF(0, Height + 1),
                Theme.Blend(under, Theme.GoldBase, 0.22f), Theme.Blend(under, Theme.GoldBase, 0.07f));
            g.FillPath(veil, tile);
            using var edge = new Pen(Theme.GoldLine, 1f);
            g.DrawPath(edge, tile);
        }

        if (IconSvg != null)
        {
            var side = Width * 0.56f;
            SvgIcon.Draw(g, IconSvg, new RectangleF((Width - side) / 2f, (Height - side) / 2f, side, side),
                Solid ? Theme.GoldInk : Theme.GoldHi, Solid ? 0.35f : 0.22f);
        }
    }
}
