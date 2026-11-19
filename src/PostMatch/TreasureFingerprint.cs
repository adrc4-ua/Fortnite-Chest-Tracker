using System.Text.Json;

namespace FCT.PostMatch;

/// <summary>
/// Tells which gizmo a treasure card shows by comparing its icon with reference icons taken from real end screens.
/// The icon is reduced to a small grid (its "print"): each cell keeps its colour as chromaticity (independent of how
/// bright the screen is) and its brightness relative to the whole card (independent of the screen's overall
/// brightness), so the same gizmo gives the same print at any resolution, size, brightness or compression, and in
/// every language (the icons carry no text). The references are prints, not images: they live in
/// treasure-prints.json, and a new one comes from any capture with <c>--read-treasure-screen</c>.
/// </summary>
internal static class TreasureFingerprint
{
    /// <summary>Cells per side of a print.</summary>
    public const int Size = 12;

    /// <summary>Part of the card box trimmed on each side: the gold frame and its glow, around the icon.</summary>
    private const double Trim = 0.14;

    /// <summary>Weight of brightness against colour when comparing two cells.</summary>
    private const double LightWeight = 0.35;

    /// <summary>
    /// The closest reference must be at most this far (measured on real end screens: the same gizmo is at most 0.084
    /// away, even from a compressed video frame or a card still flashing in; another gizmo at least 0.107, a featured
    /// back bling 0.103 from the firework)…
    /// </summary>
    private const double Accept = 0.10;

    /// <summary>
    /// …and lead the closest other gizmo by this much. A card still flashing in, or a featured treasure (an outfit, a
    /// vehicle…), is far from all of them or close to several, so it stays unknown and never gets a wrong name.
    /// </summary>
    private const double Lead = 0.025;

    private static readonly Lazy<IReadOnlyList<(string Gizmo, float[] Print)>> References = new(Load);

    /// <summary>
    /// The gizmo shown on this card, or null when no reference is clearly the one. References named
    /// <see cref="TreasureScreenReader.Unknown"/> are featured treasures already seen on real end screens (a back bling
    /// is a gold card with a small icon, like the firework): a card closest to one of them is not a gizmo, and a gizmo
    /// that is nearly as close to one is not sure enough.
    /// </summary>
    public static string? Identify(WindowCapture.Frame frame, int x, int y, int width, int height) =>
        Match(frame, x, y, width, height) is { Best: <= Accept } m && m.Next - m.Best >= Lead && m.Gizmo != TreasureScreenReader.Unknown
            ? m.Gizmo
            : null;

    /// <summary>The closest gizmo, its distance and the distance of the closest other gizmo.</summary>
    public static (string? Gizmo, double Best, double Next) Match(WindowCapture.Frame frame, int x, int y, int width, int height)
    {
        // a few small shifts: the card box found on screen can be off by a few pixels
        var prints = new List<float[]>(9);
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                prints.Add(Of(frame, x, y, width, height, dx, dy));
            }
        }
        var byGizmo = new Dictionary<string, double>();
        foreach (var (gizmo, reference) in References.Value)
        {
            var distance = prints.Min(p => Distance(p, reference));
            byGizmo[gizmo] = Math.Min(byGizmo.GetValueOrDefault(gizmo, double.MaxValue), distance);
        }
        var ranked = byGizmo.OrderBy(kv => kv.Value).ToList();
        return ranked.Count switch
        {
            0 => (null, double.MaxValue, double.MaxValue),
            1 => (ranked[0].Key, ranked[0].Value, double.MaxValue),
            _ => (ranked[0].Key, ranked[0].Value, ranked[1].Value),
        };
    }

    /// <summary>
    /// The print of the icon inside a card box: per cell, its average colour as (r, g, b) / (r + g + b), and its
    /// brightness against the card's average (halved and capped at 1). <paramref name="shiftX"/> and
    /// <paramref name="shiftY"/> move the grid by half a cell.
    /// </summary>
    public static float[] Of(WindowCapture.Frame f, int x, int y, int width, int height, int shiftX = 0, int shiftY = 0)
    {
        double left = x + width * Trim + shiftX * width / (2.0 * Size), top = y + height * Trim + shiftY * height / (2.0 * Size);
        double w = width * (1 - 2 * Trim), h = height * (1 - 2 * Trim);
        var sums = new double[Size * Size * 3];
        for (var row = 0; row < Size; row++)
        {
            int y0 = Clamp((int)(top + h * row / Size), f.Height), y1 = Math.Max(y0 + 1, Clamp((int)(top + h * (row + 1) / Size), f.Height));
            for (var col = 0; col < Size; col++)
            {
                int x0 = Clamp((int)(left + w * col / Size), f.Width), x1 = Math.Max(x0 + 1, Clamp((int)(left + w * (col + 1) / Size), f.Width));
                // at most ~8 × 8 samples per cell: plenty, and fast on a 4K screen
                int stepX = Math.Max(1, (x1 - x0) / 8), stepY = Math.Max(1, (y1 - y0) / 8);
                double r = 0, g = 0, b = 0;
                var n = 0;
                for (var py = y0; py < y1 && py < f.Height; py += stepY)
                {
                    for (var px = x0; px < x1 && px < f.Width; px += stepX)
                    {
                        var i = (py * f.Width + px) * 4;
                        b += f.Bgra[i];
                        g += f.Bgra[i + 1];
                        r += f.Bgra[i + 2];
                        n++;
                    }
                }
                var cell = (row * Size + col) * 3;
                if (n > 0)
                {
                    sums[cell] = r / n;
                    sums[cell + 1] = g / n;
                    sums[cell + 2] = b / n;
                }
            }
        }

        var mean = 1e-6;
        for (var c = 0; c < Size * Size; c++)
        {
            mean += sums[c * 3] + sums[c * 3 + 1] + sums[c * 3 + 2];
        }
        mean /= Size * Size;
        var print = new float[Size * Size * 4];
        for (var c = 0; c < Size * Size; c++)
        {
            double r = sums[c * 3], g = sums[c * 3 + 1], b = sums[c * 3 + 2], total = r + g + b + 1e-6;
            print[c * 4] = (float)(r / total);
            print[c * 4 + 1] = (float)(g / total);
            print[c * 4 + 2] = (float)(b / total);
            print[c * 4 + 3] = (float)(Math.Min(2.0, total / mean) / 2);
        }
        return print;
    }

    private static int Clamp(int value, int limit) => Math.Clamp(value, 0, Math.Max(0, limit - 1));

    /// <summary>Average difference per cell: colour distance plus a smaller part of the brightness difference.</summary>
    public static double Distance(float[] a, float[] b)
    {
        double total = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            double dr = a[i] - b[i], dg = a[i + 1] - b[i + 1], db = a[i + 2] - b[i + 2];
            total += Math.Sqrt(dr * dr + dg * dg + db * db) + LightWeight * Math.Abs(a[i + 3] - b[i + 3]);
        }
        return total / (a.Length / 4);
    }

    /// <summary>A print as text (one byte per value, base 64), the form treasure-prints.json keeps.</summary>
    public static string Encode(float[] print) =>
        Convert.ToBase64String(print.Select(v => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255)).ToArray());

    public static float[] Decode(string text) => Convert.FromBase64String(text).Select(v => v / 255f).ToArray();

    /// <summary>
    /// The reference prints (embedded in the exe): gizmo name → one print per sample screen, taken only from cards of
    /// the treasure panel (never from the screens that follow it, whose Sprite icons once made a reference that read
    /// the Sprite collection as fireworks).
    /// </summary>
    private static IReadOnlyList<(string, float[])> Load()
    {
        try
        {
            using var stream = typeof(TreasureFingerprint).Assembly.GetManifestResourceStream("FCT.treasure-prints.json");
            if (stream == null)
            {
                return Array.Empty<(string, float[])>();
            }
            using var json = JsonDocument.Parse(stream);
            var list = new List<(string, float[])>();
            foreach (var entry in json.RootElement.EnumerateArray())
            {
                var gizmo = entry.GetProperty("gizmo").GetString();
                var print = entry.GetProperty("print").GetString();
                if (gizmo != null && print != null && Decode(print) is { Length: Size * Size * 4 } decoded)
                {
                    list.Add((gizmo, decoded));
                }
            }
            return list;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return Array.Empty<(string, float[])>();
        }
    }
}
