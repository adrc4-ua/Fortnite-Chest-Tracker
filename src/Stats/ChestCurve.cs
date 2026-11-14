using System.Globalization;
using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>
/// The 30-day chest curve. The line and its fill are an SVG stretched to the card; numbers, dates, the average line and
/// the dots are HTML laid over it in percentages, so text never gets distorted at any size.
/// </summary>
internal static class ChestCurve
{
    private const double Width = 1000, Height = 300;

    public static string Build(List<DateTime> days, List<int> chests)
    {
        var top = RoundTop(chests.Max());
        double X(int index) => index * Width / (days.Count - 1);
        double Y(double value) => (1 - value / top) * Height;
        static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        string Left(double x) => Fmt(100 * x / Width) + "%";
        string Down(double y) => Fmt(100 * y / Height) + "%";

        var html = new StringBuilder($"<div class=\"curve\" role=\"img\" aria-label=\"{Esc(L.T("Cofres por día, últimos 30 días", "Chests per day, last 30 days"))}\">");
        foreach (var level in new[] { 0, top / 2, top })
        {
            html.Append($"<span class=\"c-grid\" style=\"top:{Down(Y(level))}\"></span><span class=\"c-y\" style=\"top:{Down(Y(level))}\">{Num(level)}</span>");
        }
        var playedDays = chests.Where(c => c > 0).ToList();
        if (playedDays.Count > 0)
        {
            var average = playedDays.Average();
            html.Append($"<span class=\"c-avg\" style=\"top:{Down(Y(average))}\"></span>"); // its figure is in the section's legend
        }

        var points = chests.Select((value, i) => (X: X(i), Y: Y(value))).ToList();
        var line = SmoothPath(points, Fmt);
        html.Append($"<svg viewBox=\"0 0 {Fmt(Width)} {Fmt(Height)}\" preserveAspectRatio=\"none\" aria-hidden=\"true\">")
            .Append("<defs><linearGradient id=\"evoFill\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#e8b04b\" stop-opacity=\".32\"/><stop offset=\"1\" stop-color=\"#e8b04b\" stop-opacity=\"0\"/></linearGradient></defs>")
            .Append($"<path class=\"c-area\" d=\"{line} L{Fmt(Width)},{Fmt(Height)} L0,{Fmt(Height)} Z\"/><path class=\"c-line\" d=\"{line}\"/></svg>");

        var last = points.Count - 1;
        for (var i = 0; i <= last; i++)
        {
            var isToday = i == last;
            if (chests[i] > 0)
            {
                var tip = $"{days[i].ToString(L.En ? "ddd MMM d" : "ddd d MMM", L.Culture)} · {L.Count(chests[i], "cofre", "cofres", "chest", "chests")}";
                html.Append($"<span class=\"c-dot{(isToday ? " is-now" : "")}\" style=\"left:{Left(points[i].X)};top:{Down(points[i].Y)}\" title=\"{Esc(tip)}\"></span>");
            }
            if (isToday || (last - i) % 5 == 0) // a date every five days, counted back from today
            {
                var label = ShortDay(days[i]); // today: its own date, in gold
                html.Append($"<span class=\"c-x{(isToday ? " is-now" : "")}\" style=\"left:{Left(points[i].X)}\">{Esc(label)}</span>");
            }
        }
        return html.Append("</div>").ToString();
    }

    /// <summary>A smooth curve through the points (Catmull-Rom as cubic Béziers), never dipping below zero.</summary>
    private static string SmoothPath(List<(double X, double Y)> points, Func<double, string> fmt)
    {
        static double Floor(double y) => Math.Min(Height, Math.Max(0, y));
        var path = new StringBuilder($"M{fmt(points[0].X)},{fmt(points[0].Y)}");
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var before = points[Math.Max(0, i - 1)];
            var from = points[i];
            var to = points[i + 1];
            var after = points[Math.Min(points.Count - 1, i + 2)];
            path.Append($" C{fmt(from.X + (to.X - before.X) / 6)},{fmt(Floor(from.Y + (to.Y - before.Y) / 6))} {fmt(to.X - (after.X - from.X) / 6)},{fmt(Floor(to.Y - (after.Y - from.Y) / 6))} {fmt(to.X)},{fmt(to.Y)}");
        }
        return path.ToString();
    }

    /// <summary>A round top for the scale (1, 2, 2.5 or 5 × 10ⁿ, made even), at least the value; 4 for tiny values.</summary>
    private static int RoundTop(int value)
    {
        if (value <= 4)
        {
            return 4;
        }
        var power = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (step * power >= value)
            {
                var round = (int)Math.Round(step * power);
                return round % 2 == 0 ? round : round + 1;
            }
        }
        return value;
    }
}
