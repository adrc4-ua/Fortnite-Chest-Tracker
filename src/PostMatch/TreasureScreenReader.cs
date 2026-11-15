namespace FCT.PostMatch;

/// <summary>
/// Reads the "Treasure found" panel of the post-match screen: finds the gold treasure cards and tells which gizmo
/// each one shows from the colours of its icon. The screen shows icons only (no names), in the order they were won.
/// <list type="bullet">
/// <item>Extraction Accelerator: green device.</item>
/// <item>Lucky Locator: purple triangle. Sprite Locator: red triangle.</item>
/// <item>Portable Extractor: capsule with a steel-blue frame.</item>
/// <item>Golden Firework: pale gold / white firework burst.</item>
/// </list>
/// Anything else on a gold card (a featured treasure: outfit, vehicle…) is reported as <see cref="Unknown"/>.
/// </summary>
public static class TreasureScreenReader
{
    public const string Unknown = "?";

    public sealed record Card(int X, int Y, int Width, int Height, string Item);

    /// <summary>Gold cards of the treasure panel, left to right (the order the treasures were won).</summary>
    public static List<Card> Read(WindowCapture.Frame frame)
    {
        var cards = FindCards(frame);
        return cards.Select(c => c with { Item = Classify(frame, c) }).ToList();
    }

    // ---- Cards: connected areas of the saturated gold of the card background ----------------------------------------

    private static bool IsGold(byte r, byte g, byte b)
    {
        var (h, s, v) = Hsv(r, g, b);
        return h is >= 0.07 and <= 0.16 && s >= 0.45 && v >= 0.70;
    }

    /// <summary>
    /// Any shade of the card: its saturated gold, the lighter and darker golds of its glow and its pale border. With
    /// only the saturated gold, a big icon (the diagonal capsule of the Extraction Accelerator, the rays of a
    /// firework) splits the card into pieces that no longer look like a card.
    /// </summary>
    private static bool IsCardShade(byte r, byte g, byte b)
    {
        var (h, s, v) = Hsv(r, g, b);
        return h is >= 0.05 and <= 0.18 && s >= 0.25 && v >= 0.55;
    }

    private static List<Card> FindCards(WindowCapture.Frame f)
    {
        // Work on a ~480 px wide grid: fast, and plenty for cards that are ~5 % of the screen wide.
        var step = Math.Max(1, f.Width / 480);
        int gw = f.Width / step, gh = f.Height / step;
        // The panel is in the upper left part of the screen: nothing else there is gold (the victory banner is to the
        // right of it, the outfit of the battle pass ad below it).
        int limitX = (int)(gw * 0.34), limitY = (int)(gh * 0.52);
        var gold = new bool[gw * gh];
        for (var y = 0; y < limitY; y++)
        {
            for (var x = 0; x < limitX; x++)
            {
                var i = ((y * step) * f.Width + x * step) * 4;
                gold[y * gw + x] = IsCardShade(f.Bgra[i + 2], f.Bgra[i + 1], f.Bgra[i]);
            }
        }

        // Connected areas of card shades (each with its box and size, in grid cells).
        var seen = new bool[gw * gh];
        var pieces = new List<(int X0, int Y0, int X1, int Y1, int Count)>();
        var stack = new Stack<int>();
        for (var start = 0; start < gold.Length; start++)
        {
            if (!gold[start] || seen[start])
            {
                continue;
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0, count = 0;
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                int px = p % gw, py = p / gw;
                count++;
                minX = Math.Min(minX, px); maxX = Math.Max(maxX, px);
                minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
                foreach (var (nx, ny) in new[] { (px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1) })
                {
                    if (nx >= 0 && nx < gw && ny >= 0 && ny < gh && gold[ny * gw + nx] && !seen[ny * gw + nx])
                    {
                        seen[ny * gw + nx] = true;
                        stack.Push(ny * gw + nx);
                    }
                }
            }
            if (count >= 8)
            {
                pieces.Add((minX, minY, maxX, maxY, count));
            }
        }

        // Pieces of one card: an icon that crosses the whole card (the diagonal capsule) leaves it in two pieces whose
        // boxes overlap. Cards next to each other are ~25 px apart, so their boxes never do.
        for (var merged = true; merged;)
        {
            merged = false;
            for (var i = 0; i < pieces.Count && !merged; i++)
            {
                for (var j = i + 1; j < pieces.Count; j++)
                {
                    var (a, b) = (pieces[i], pieces[j]);
                    if (a.X0 <= b.X1 && b.X0 <= a.X1 && a.Y0 <= b.Y1 && b.Y0 <= a.Y1)
                    {
                        pieces[i] = (Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1), a.Count + b.Count);
                        pieces.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
            }
        }

        var cards = new List<Card>();
        foreach (var (x0, y0, x1, y1, count) in pieces)
        {
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            var relative = bw * step / (double)f.Width;
            var aspect = bw / (double)bh;
            var fill = count / (double)(bw * bh);
            if (count > 60 && aspect is > 0.6 and < 1.6 && relative is > 0.035 and < 0.25 && fill > 0.25)
            {
                cards.Add(new Card(x0 * step, y0 * step, bw * step, bh * step, Unknown));
            }
        }

        // Only the treasure panel (left part of the screen), and no card inside another one (glows inside an icon).
        cards = cards.Where(c => c.X + c.Width / 2 <= f.Width * 0.6).ToList();
        cards = cards.Where(c => !cards.Any(o => !ReferenceEquals(o, c) && o.X <= c.X && o.Y <= c.Y
                                                  && o.X + o.Width >= c.X + c.Width && o.Y + o.Height >= c.Y + c.Height)).ToList();
        // One row of cards: keep the biggest group at the same height (other gold bits of the screen are dropped).
        if (cards.Count > 1)
        {
            var row = cards.GroupBy(c => cards.Count(o => Math.Abs(o.Y + o.Height / 2 - (c.Y + c.Height / 2)) < c.Height / 2))
                .OrderByDescending(g => g.Key).First().Key;
            cards = cards.Where(c => cards.Count(o => Math.Abs(o.Y + o.Height / 2 - (c.Y + c.Height / 2)) < c.Height / 2) == row).ToList();
        }
        return cards.OrderBy(c => c.X).ToList();
    }

    // ---- Icon: share of each colour family inside the card --------------------------------------------------------

    /// <summary>
    /// Each gizmo's colour family, and the smallest share of the card it has to cover. Low enough for a card caught
    /// while it is still fading or flashing in (measured on real end screens, darker, lighter, washed out and at half
    /// resolution): the firework's rays are pale gold rather than white then.
    /// </summary>
    private static readonly (string Family, double Floor, string Gizmo)[] Families =
    {
        ("green", 0.03, "Extraction Accelerator"),
        ("purple", 0.03, "Lucky Locator"),
        ("red", 0.05, "Sprite Locator"),
        ("steel", 0.05, "Portable Extractor"),
        ("pale", 0.03, "Golden Firework"),
    };

    /// <summary>
    /// The landing flash washes any icon into pale gold, so a mid-flash card used to read as the firework. The real
    /// firework's rays cover at most ~0.16 of the card (measured on real end screens); a washed-out icon covers 0.28
    /// to 0.43. Above this the card is still flashing and stays unknown, to be outvoted by the settled captures.
    /// </summary>
    private const double PaleCap = 0.24;

    /// <summary>
    /// The gizmo whose colour dominates the icon. A gizmo icon is mostly one colour; a featured treasure (an outfit, a
    /// vehicle…) mixes several, so the winner must clearly lead the runner-up or the card stays <see cref="Unknown"/>.
    /// </summary>
    private static string Classify(WindowCapture.Frame f, Card c)
    {
        int ix = (int)(c.Width * 0.12), iy = (int)(c.Height * 0.12);
        int x0 = c.X + ix, y0 = c.Y + iy, w = c.Width - 2 * ix, h = c.Height - 2 * iy;
        if (w <= 4 || h <= 4)
        {
            return Unknown;
        }
        var counts = new Dictionary<string, int> { ["green"] = 0, ["purple"] = 0, ["red"] = 0, ["steel"] = 0, ["pale"] = 0 };
        const int samples = 64;
        for (var sy = 0; sy < samples; sy++)
        {
            for (var sx = 0; sx < samples; sx++)
            {
                var i = ((y0 + sy * h / samples) * f.Width + x0 + sx * w / samples) * 4;
                if (FamilyOf(f.Bgra[i + 2], f.Bgra[i + 1], f.Bgra[i]) is { } family)
                {
                    counts[family]++;
                }
            }
        }
        double Share(string family) => counts[family] / (double)(samples * samples);
        var ranked = Families.OrderByDescending(g => Share(g.Family) / g.Floor).ToList();
        var (best, runnerUp) = (ranked[0], ranked[1]);
        if (best.Family == "pale" && Share("pale") > PaleCap)
        {
            return Unknown; // a card still flashing in, not the firework
        }
        var clearLead = Share(best.Family) / best.Floor >= 2 * Share(runnerUp.Family) / runnerUp.Floor;
        return Share(best.Family) >= best.Floor && clearLead ? best.Gizmo : Unknown;
    }

    /// <summary>The colour family of one pixel of an icon; null for the card's gold, dark shading or anything else.</summary>
    private static string? FamilyOf(byte r, byte g, byte b)
    {
        if (IsGold(r, g, b))
        {
            return null;
        }
        var (hue, s, v) = Hsv(r, g, b);
        if (hue is >= 0.50 and <= 0.72 && s is >= 0.05 and <= 0.65 && v is >= 0.12 and <= 0.70) return "steel";
        if (v < 0.30) return null;
        // the tinted families before "pale", so a light lilac or mint pixel keeps its hue instead of counting as white
        if (hue is >= 0.20 and <= 0.45 && s > 0.30) return "green";
        if (hue is >= 0.66 and <= 0.93 && s > 0.25) return "purple";
        if ((hue >= 0.94 || hue <= 0.04) && s > 0.45) return "red";
        if (s < 0.30 && v >= 0.55) return "pale";
        return null;
    }

    private static (double H, double S, double V) Hsv(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var d = max - min;
        double h = 0;
        if (d > 0)
        {
            h = max == rf ? ((gf - bf) / d) % 6 : max == gf ? (bf - rf) / d + 2 : (rf - gf) / d + 4;
            h /= 6;
            if (h < 0) h += 1;
        }
        return (h, max == 0 ? 0 : d / max, max);
    }
}
