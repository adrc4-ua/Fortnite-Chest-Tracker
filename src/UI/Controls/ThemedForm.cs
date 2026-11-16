using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// Base of every FCT window: the black background with its gold glow, a dark title bar (black with a gold edge on
/// Windows 11) and double buffering against flicker.
/// </summary>
[SupportedOSPlatform("windows")]
public class ThemedForm : Form, IBackdrop
{
    public ThemedForm()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle);
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Backdrop.Glow(e.Graphics, ClientSize, Point.Empty);

    void IBackdrop.PaintBackdrop(Graphics g, Control child) =>
        Backdrop.Glow(g, ClientSize, PointToClient(child.Parent!.PointToScreen(child.Location)));
}
