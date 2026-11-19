namespace FCT.PostMatch;

/// <summary>
/// Reads the "Treasure found" panel of the post-match screen: finds the gold treasure cards and tells which gizmo
/// each one shows by comparing its icon with reference icons of real end screens (<see cref="TreasureFingerprint"/>).
/// The screen shows icons only (no names, so it reads the same in every language), in the order they were won.
/// Anything else on a gold card (a featured treasure: outfit, vehicle…), or a card still flashing in, is reported as
/// <see cref="Unknown"/>.
/// </summary>
public static class TreasureScreenReader
{
    public const string Unknown = "?";

    public sealed record Card(int X, int Y, int Width, int Height, string Item);

    /// <summary>Gold cards of the treasure panel, left to right (the order the treasures were won).</summary>
    public static List<Card> Read(WindowCapture.Frame frame)
    {
        var cards = FindCards(frame);
        return cards.Select(c => c with { Item = TreasureFingerprint.Identify(frame, c.X, c.Y, c.Width, c.Height) ?? Unknown }).ToList();
    }

    // ---- Cards: connected areas of the saturated gold of the card background ----------------------------------------

    /// <summary>
    /// The hue and saturation of any shade of the card: its saturated gold, the lighter and darker golds of its glow
    /// and its pale border. With only the saturated gold, a big icon (the diagonal capsule of the Extraction
    /// Accelerator, the rays of a firework) splits the card into pieces that no longer look like a card.
    /// </summary>
    private static bool IsCardHue(double h, double s) => h is >= 0.05 and <= 0.18 && s >= 0.25;

    /// <summary>
    /// How bright a card shade must be: 0.55, or less on a darker screen (gamma, HDR, a darker capture), where it is
    /// measured against the brightest golds of the panel.
    /// </summary>
    private const double CardBrightness = 0.55;

    private static List<Card> FindCards(WindowCapture.Frame f)
    {
        // The game lays its menus out on a 16:9 area: the whole screen on 16:9 and narrower ones, a centred part of a
        // wider one (ultrawide), where the panel may also stay at the left edge. Sizes are measured on that area.
        var area = Math.Min(f.Width, f.Height * 16.0 / 9);
        var side = (f.Width - area) / 2;
        // Work on a ~480 px wide grid: fast, and plenty for cards that are ~5 % of the area wide.
        var step = Math.Max(1, (int)(area / 480));
        int gw = f.Width / step, gh = f.Height / step;
        // The panel is in the upper left part of the area: nothing else there is gold (the victory banner is to the
        // right of it, the outfit of the battle pass ad below it).
        int limitX = Math.Min(gw, (int)((side + area * 0.34) / step)), limitY = (int)(gh * 0.52);
        var gold = new bool[gw * gh];
        var brightness = new float[gw * gh];
        var golds = new List<float>();
        for (var y = 0; y < limitY; y++)
        {
            for (var x = 0; x < limitX; x++)
            {
                var i = ((y * step) * f.Width + x * step) * 4;
                var (h, s, v) = Hsv(f.Bgra[i + 2], f.Bgra[i + 1], f.Bgra[i]);
                if (IsCardHue(h, s))
                {
                    brightness[y * gw + x] = (float)v;
                    golds.Add((float)v);
                }
            }
        }
        // the brightest golds (95th percentile) stand for the card's own gold; its shades go down to 60 % of it
        var least = CardBrightness;
        if (golds.Count >= 50)
        {
            golds.Sort();
            least = Math.Min(CardBrightness, 0.6 * golds[(int)(golds.Count * 0.95)]);
        }
        for (var i = 0; i < gold.Length; i++)
        {
            gold[i] = brightness[i] > 0 && brightness[i] >= least;
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

        // Pieces of one card: an icon that crosses the whole card (the diagonal capsule) leaves it in two pieces, whose
        // boxes overlap or stand a few pixels apart. Cards next to each other are 1.25 % of the area apart (24 px on a
        // 1920 px wide screen): pieces closer than half that, side by side or one above the other, are one card.
        var near = Math.Max(1, (int)Math.Round(area * 0.006 / step)) + 1; // in grid cells, with the step between them
        for (var merged = true; merged;)
        {
            merged = false;
            for (var i = 0; i < pieces.Count && !merged; i++)
            {
                for (var j = i + 1; j < pieces.Count; j++)
                {
                    var (a, b) = (pieces[i], pieces[j]);
                    if (a.X0 <= b.X1 + near && b.X0 <= a.X1 + near && a.Y0 <= b.Y1 + near && b.Y0 <= a.Y1 + near)
                    {
                        pieces[i] = (Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1), a.Count + b.Count);
                        pieces.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
            }
        }

        // A card is 5.2 to 5.6 % of the area wide (up to 5.8 % while it flashes in) and a little wider than tall
        // (measured on real end screens). The screens that follow it when the player leaves (the Sprite collection,
        // loading screens, the lobby) have yellow art that looks like a card for many captures in a row, but always
        // narrower (at most 4.8 %) or clearly taller or flatter.
        var cards = new List<Card>();
        foreach (var (x0, y0, x1, y1, count) in pieces)
        {
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            var relative = bw * step / area;
            var aspect = bw / (double)bh;
            var fill = count / (double)(bw * bh);
            if (count > 60 && aspect is > 0.9 and < 1.3 && relative is > 0.0495 and < 0.075 && fill > 0.25)
            {
                cards.Add(new Card(x0 * step, y0 * step, bw * step, bh * step, Unknown));
            }
        }

        // Only the treasure panel (left part of the area, below the title strip where the placement banner is), and no
        // card inside another one (glows inside an icon).
        cards = cards.Where(c => c.X + c.Width / 2 <= side + area * 0.6 && c.Y + c.Height / 2 >= f.Height * 0.2).ToList();
        cards = cards.Where(c => !cards.Any(o => !ReferenceEquals(o, c) && o.X <= c.X && o.Y <= c.Y
                                                  && o.X + o.Width >= c.X + c.Width && o.Y + o.Height >= c.Y + c.Height)).ToList();
        // One row of cards: keep the biggest group at the same height (other gold bits of the screen are dropped).
        if (cards.Count > 1)
        {
            var row = cards.GroupBy(c => cards.Count(o => Math.Abs(o.Y + o.Height / 2 - (c.Y + c.Height / 2)) < c.Height / 2))
                .OrderByDescending(g => g.Key).First().Key;
            cards = cards.Where(c => cards.Count(o => Math.Abs(o.Y + o.Height / 2 - (c.Y + c.Height / 2)) < c.Height / 2) == row).ToList();
        }
        return InPanel(cards.OrderBy(c => c.X).ToList(), area, side);
    }

    /// <summary>
    /// The cards of the panel: the first one stands at its left margin (5.8 % of the area from the area's left, or
    /// from the screen's left edge when an ultrawide screen keeps the panel there), and each next one right after the
    /// previous one (1.25 % of the area apart). Gold bits elsewhere (art of a loading screen or the lobby) are dropped.
    /// </summary>
    private static List<Card> InPanel(List<Card> row, double area, double side)
    {
        static bool AtMargin(double x, double area) => x >= area * 0.015 && x <= area * 0.11;
        var first = row.FindIndex(c => AtMargin(c.X - side, area) || AtMargin(c.X, area));
        if (first < 0)
        {
            return new List<Card>();
        }
        var panel = new List<Card> { row[first] };
        foreach (var c in row.Skip(first + 1))
        {
            var previous = panel[^1];
            if (c.X - (previous.X + previous.Width) > area * 0.04)
            {
                break;
            }
            panel.Add(c);
        }
        return panel;
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
