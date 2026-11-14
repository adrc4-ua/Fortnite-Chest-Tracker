using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// The icon in the notification area: the app stays there while its window is hidden. A left click opens the window;
/// the right click shows the menu.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TrayIcon : IDisposable
{
    /// <summary>Icons of the menu, in logical pixels (drawn at the screen's scale when the menu opens).</summary>
    private const int IconSize = 16;

    private readonly NotifyIcon _icon;

    public TrayIcon(Icon? icon, Action open, Action overlay, Action stats, Action settings, Action about, Action exit)
    {
        _icon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = AppInfo.Name,
            ContextMenuStrip = Menu(icon, open, overlay, stats, settings, about, exit),
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                open();
            }
        };
    }

    /// <summary>
    /// The menu, in groups like a launcher's: open the app; its three places; about; exit. Each entry has its icon in gold
    /// (the app's own drawings; the app's logo for "Open").
    /// </summary>
    internal static ContextMenuStrip Menu(Icon? icon, Action open, Action overlay, Action stats, Action settings, Action about, Action exit)
    {
        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ForeColor = Theme.Ink, Font = Theme.Text(9f), ShowImageMargin = true };
        var openItem = Item(L.F("Abrir {0}", "Open {0}", AppInfo.ShortName), open);
        openItem.Font = new Font(menu.Font, FontStyle.Bold);
        var entries = new (ToolStripItem Item, string? Svg)[]
        {
            (openItem, null),
            (new ToolStripSeparator(), null),
            (Item(L.T("Overlay", "Overlay"), overlay), Icons.Overlay),
            (Item(L.T("Estadísticas", "Stats"), stats), Icons.Summary),
            (Item(L.T("Configuración", "Settings"), settings), Icons.Settings),
            (new ToolStripSeparator(), null),
            (Item(L.T("Acerca de y licencia", "About and license"), about), Icons.Info),
            (new ToolStripSeparator(), null),
            (Item(L.T("Salir", "Exit"), exit), Icons.Power),
        };
        foreach (var (item, _) in entries)
        {
            item.ForeColor = Theme.Ink;
            menu.Items.Add(item);
        }

        // the icons at the scale of the screen the menu opens on, so they stay sharp (drawn again if it changes)
        var drawnFor = 0;
        menu.Opening += (_, _) =>
        {
            var dpi = menu.DeviceDpi > 0 ? menu.DeviceDpi : 96;
            if (dpi == drawnFor)
            {
                return;
            }
            drawnFor = dpi;
            var size = (int)Math.Round(IconSize * dpi / 96f);
            menu.ImageScalingSize = new Size(size, size);
            foreach (var (item, svg) in entries)
            {
                if (item is not ToolStripMenuItem entry)
                {
                    continue;
                }
                var previous = entry.Image;
                entry.Image = svg != null ? Gold(svg, size) : Logo(icon, size);
                previous?.Dispose();
            }
        };
        return menu;
    }

    private static ToolStripMenuItem Item(string text, Action action) =>
        new(text, null, (_, _) => action()) { Padding = new Padding(0, 3, 0, 3) };

    /// <summary>An icon of the app drawn in gold on a transparent square.</summary>
    private static Bitmap Gold(string svg, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        SvgIcon.Draw(g, svg, new RectangleF(0, 0, size, size), Theme.GoldBase, 0.3f);
        return bitmap;
    }

    /// <summary>The app's logo (the chest, embedded in the exe) at that size; a gold chest if it cannot be read.</summary>
    private static Bitmap Logo(Icon? icon, int size)
    {
        using var embedded = typeof(TrayIcon).Assembly.GetManifestResourceStream("FCT.app.ico");
        if (embedded == null && icon == null)
        {
            return Gold(Icons.Chest, size);
        }
        using var sized = embedded != null ? new Icon(embedded, new Size(size, size)) : new Icon(icon!, new Size(size, size));
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawIcon(sized, new Rectangle(0, 0, size, size));
        return bitmap;
    }

    /// <summary>A Windows notification from the tray icon (Windows holds it while a game is in fullscreen).</summary>
    public void ShowTip(string title, string text) => _icon.ShowBalloonTip(10_000, title, text, ToolTipIcon.Info);

    /// <summary>Hidden first: otherwise the icon lingers in the tray until the mouse passes over it.</summary>
    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
