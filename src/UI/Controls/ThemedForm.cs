using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// Base of every FCT window: the black background with its gold glow, a dark title bar (black with a gold edge on
/// Windows 11) and double buffering against flicker.
/// <para>
/// No white flashes: Windows shows a new (or hidden and shown again) window before it has painted, which shows as a
/// white frame for a few milliseconds. The window is kept cloaked by the compositor while it paints everything, and only
/// then revealed. Areas that appear when it grows are filled with the app's background instead of left blank.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class ThemedForm : Form, IBackdrop
{
    private const int WmEraseBkgnd = 0x0014, DwmCloak = 13;
    private bool _cloaked;

    public ThemedForm()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmEraseBkgnd)
        {
            // the paint that follows draws the glow; until then, the app's background rather than nothing (white)
            if (m.WParam != IntPtr.Zero)
            {
                using var g = Graphics.FromHdc(m.WParam);
                g.Clear(Theme.Bg);
            }
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle);
        Cloak(true); // hidden from view until its first paint is done
    }

    protected override void SetVisibleCore(bool value)
    {
        if (value && IsHandleCreated && !Visible)
        {
            Cloak(true); // shown again (from the tray): its old image is gone, so it paints hidden as well
        }
        base.SetVisibleCore(value);
        if (value && _cloaked && IsHandleCreated)
        {
            // after the handlers of showing (Load, Shown) have filled it in
            BeginInvoke(Reveal);
        }
    }

    /// <summary>Paints the window and every control in it now, then lets it be seen.</summary>
    private void Reveal()
    {
        if (IsDisposed || !IsHandleCreated || !_cloaked)
        {
            return;
        }
        const uint Invalidate = 0x0001, Erase = 0x0004, AllChildren = 0x0080, UpdateNow = 0x0100, Frame = 0x0400;
        RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, Invalidate | Erase | AllChildren | UpdateNow | Frame);
        Cloak(false);
    }

    private void Cloak(bool on)
    {
        try
        {
            var value = on ? 1 : 0;
            _cloaked = DwmSetWindowAttribute(Handle, DwmCloak, ref value, sizeof(int)) == 0 && on;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _cloaked = false; // no desktop composition: shown as usual
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Backdrop.Glow(e.Graphics, ClientSize, Point.Empty);

    void IBackdrop.PaintBackdrop(Graphics g, Control child) =>
        Backdrop.Glow(g, ClientSize, PointToClient(child.Parent!.PointToScreen(child.Location)));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr update, IntPtr region, uint flags);
}
