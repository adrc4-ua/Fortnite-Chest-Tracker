using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FCT.Stats;

/// <summary>
/// FCT's icon as a PNG data URI for the stats page header and its browser tab, and the open chest with the pirate
/// token for the overview once the player has it. Rendered once at 96 px (crisp at 48 px on high-DPI screens, yet
/// small enough for a page that updates itself). Null when unavailable: the page then draws a chest instead.
/// </summary>
internal static class AppLogo
{
    private const int Pixels = 96;
    private static readonly Lazy<string?> Cached = new(() => Render("FCT.app.ico"));
    private static readonly Lazy<string?> TokenCached = new(() => Render("FCT.token-chest.ico"));

    public static string? DataUri => Cached.Value;

    /// <summary>The open chest with the pirate token (the Pirate Shop Access), for the overview of a player who has it.</summary>
    public static string? TokenChest => TokenCached.Value;

    private static string? Render(string resource)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        using var embedded = typeof(AppLogo).Assembly.GetManifestResourceStream(resource);
        if (embedded == null)
        {
            return null;
        }
        try
        {
            using var icon = new Icon(embedded, new Size(256, 256));
            using var large = icon.ToBitmap();
            using var small = new Bitmap(Pixels, Pixels);
            using (var canvas = Graphics.FromImage(small))
            {
                canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
                canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                canvas.DrawImage(large, 0, 0, Pixels, Pixels);
            }
            using var png = new MemoryStream();
            small.Save(png, ImageFormat.Png);
            return $"data:image/png;base64,{Convert.ToBase64String(png.ToArray())}";
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or IOException)
        {
            return null;
        }
    }
}
