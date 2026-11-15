using System.Globalization;
using System.Net;

namespace FCT.Stats;

/// <summary>Small pieces every part of the stats page uses: escaping, numbers, dates and the embedded CSS/JS.</summary>
internal static class PageParts
{
    public static string StyleSheet => Asset("stats.css");
    public static string Script => Asset("stats.js");
    public static string OverlayCss => Asset("overlay.css");

    private static readonly Dictionary<string, string> Loaded = new();

    /// <summary>A file of Stats/Page embedded in the exe (line endings normalized, whatever git did to them).</summary>
    private static string Asset(string name)
    {
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(name, out var text))
            {
                using var stream = typeof(PageParts).Assembly.GetManifestResourceStream("FCT.page." + name)
                                   ?? throw new InvalidOperationException("missing page asset " + name);
                text = new StreamReader(stream).ReadToEnd().Replace("\r\n", "\n").TrimEnd('\n');
                Loaded[name] = text;
            }
            return text;
        }
    }

    public static string Esc(string? text) => WebUtility.HtmlEncode(text ?? "");

    /// <summary>"1.234" in the page's culture.</summary>
    public static string Num(int value) => value.ToString("#,0", L.Culture);

    /// <summary>A percentage for CSS widths (always with a dot).</summary>
    public static string Css(double percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static DateTime LocalStart(MatchRecord match) => (match.MatchStartUtc ?? match.AnalyzedAtUtc).ToLocalTime();

    /// <summary>"12 nov" / "Nov 12".</summary>
    public static string ShortDay(DateTime day) => day.ToString(L.En ? "MMM d" : "d MMM", L.Culture);

    public const string Dash = "<span class=\"muted\">—</span>";

    /// <summary>A section with its diamond title, an optional right-hand note and its body.</summary>
    /// <param name="headExtra">Something more on the title's line, at its right (a button).</param>
    public static string Section(string title, string? meta, string body, string? sectionClass = null, string? headExtra = null) =>
        $"<section{(sectionClass == null ? "" : $" class=\"{sectionClass}\"")}><div class=\"sec-head\"><h2>{title}</h2>"
        + (meta == null && headExtra == null ? "" : $"<span class=\"sec-meta\">{meta}{headExtra}</span>") + $"</div>{body}</section>";

    /// <summary>The head of each tab: gold icon tile, title and a line saying what is in it.</summary>
    /// <param name="figures">Small figures shown at the right of the title (the tab's key numbers), or null.</param>
    public static string Head(string tab, string title, string subtitle, string? figures = null) =>
        $"<header class=\"head\"><span class=\"head-icon\">{PageIcons.Tabs.GetValueOrDefault(tab)}</span><div><h1>{title}</h1><p>{subtitle}</p></div>"
        + (figures == null ? "" : $"<div class=\"head-figs\">{figures}</div>") + "</header>";

    /// <summary>A labelled bar of a list (kinds, sources…).</summary>
    public static string Bar(string label, string number, double widthPercent, string? rowClass = null, bool labelHasPill = false) =>
        $"<div class=\"bar{(rowClass == null ? "" : " " + rowClass)}\"><span{(labelHasPill ? " class=\"bar-lbl\"" : "")}>{label}</span><span class=\"bar-n\">{number}</span><div class=\"meter\"><i style=\"width:{Css(widthPercent)}\"></i></div></div>";
}
