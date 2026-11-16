using System.ComponentModel;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// A card of the page: round corners, a gold veil at the top and a thin gold edge. <see cref="Hero"/> is the main card
/// (glow from the corner, stronger edge).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
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

    /// <summary>Corner radius in pixels.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 18;

    /// <summary>The main card (like the page's "Cofres abiertos").</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Hero { get; set; }

    /// <summary>Another edge colour; null for the thin gold one.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? Edge { get; set; }

    /// <summary>Plain colour without the gold veil (behind text boxes).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Flat { get; set; }

    /// <summary>The card itself, at its own origin (also painted behind its rounded children).</summary>
    internal void PaintFace(Graphics g) => Theme.PaintCard(g, Rounded.Inner(this), Radius, BackColor, Hero, Edge, Flat);

    protected override void OnPaintBackground(PaintEventArgs e) => Backdrop.Fill(this, e.Graphics);

    protected override void OnPaint(PaintEventArgs e) => PaintFace(e.Graphics);
}
