using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>
/// estadisticas.html: one self-contained page (style, script and logo inside) with a section per tab. It is rewritten
/// after every change and reloads itself, so an open browser tab stays up to date.
/// </summary>
public static class StatsPage
{
    /// <summary>Set while a batch of replays is being read: a loading card, and a faster refresh.</summary>
    public static volatile ScanProgress? Progress;

    /// <summary>
    /// The settings a page is made with, as a short mark: the page carries it, and the local server tells the current
    /// one, so a page open in the browser reloads by itself when the app changes any of them.
    /// </summary>
    public static string SettingsMark(Config config) =>
        string.Concat(new[] { config.ShowTeammates, config.CountTeammateChests, config.CountAmmoBoxes, config.CountFoodBoxes, config.CountLootCannons, config.CountRanked }
            .Select(on => on ? '1' : '0')) + (config.Language ?? "");

    /// <summary>Writes the stats page (with the app's settings and language), overlay.html and matches.csv.</summary>
    public static void WriteAll(Store store, Config config, TrackerData data)
    {
        try
        {
            var pageSettings = config.ForStatsPage();
            using (L.Scope(config.Language))
            {
                var view = Profiles.View(data, pageSettings);
                var page = Build(pageSettings, view, Profiles.InitialChests(data, pageSettings), Profiles.AllMatches(data, pageSettings, view));
                var stamp = Stamp(page);
                SaveText(store.StatsPath, page.Replace(StampSlot, $"data-stamp=\"{stamp}\"", StringComparison.Ordinal));
                ObsServer.StatsStamp = stamp;
                ObsServer.StatsLang = L.Code;
                ObsServer.StatsSettings = SettingsMark(pageSettings);
            }
            if (File.Exists(store.LegacyStatsPath))
            {
                try { File.Delete(store.LegacyStatsPath); } catch (IOException) { /* open in a browser: next time */ } catch (UnauthorizedAccessException) { }
            }
            var appView = Profiles.View(data, config);
            SaveText(store.OverlayPath, OverlayPage.File(OverlayModel.Build(appView, config)));
            SaveText(store.CsvPath, MatchesCsv.Build(data, config));
        }
        catch (Exception ex)
        {
            Console.WriteLine(L.F("No se pudo escribir el resumen: {0}", "Cannot write the summary: {0}", ex.Message));
        }
    }

    /// <summary>Where the page's mark goes: worked out from the page itself, so it is written in afterwards.</summary>
    private const string StampSlot = "data-stamp=\"\"";

    /// <summary>
    /// A short mark of the page's content: the same page always gets the same mark, so an open page only updates when
    /// something on it really changed.
    /// </summary>
    private static string Stamp(string page) =>
        Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(page)))[..16];

    /// <summary>Writes overlay.html alone (at every figure change of a live match). Failing is not worth a word.</summary>
    public static void WriteOverlay(Store store, OverlayView view)
    {
        try
        {
            SaveText(store.OverlayPath, OverlayPage.File(view));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static void SaveText(string path, string text)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>The page for these (already filtered and adjusted) matches.</summary>
    /// <param name="allMatches">Every match of the account, those before FCT included (the analyzed ones if lower).</param>
    public static string Build(Config config, TrackerData data, int initialChests = 0, int allMatches = 0)
    {
        var page = new PageData(config, data, initialChests, allMatches);
        var refresh = Progress != null ? 2 : 15;
        var logo = AppLogo.DataUri;
        var html = new StringBuilder(300_000);

        html.Append("<!doctype html><html lang=\"").Append(L.Code).Append("\"><head><meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<meta name=\"color-scheme\" content=\"dark\">\n")
            .Append($"<noscript><meta http-equiv=\"refresh\" content=\"{refresh}\"></noscript>\n")
            .Append("<title>").Append(AppInfo.Name).Append(page.Player.Length > 0 ? " · " + Esc(page.Player) : "").Append("</title>")
            .Append(logo == null ? "" : $"<link rel=\"icon\" href=\"{logo}\">")
            .Append("<style>").Append(StyleSheet).Append("</style></head>\n")
            .Append($"<body {StampSlot} data-refresh=\"{refresh}\" data-settings=\"{Esc(SettingsMark(config))}\" data-saved=\"{Esc(L.T("Guardado", "Saved"))}\" data-failed=\"{Esc(L.T("No se pudo guardar", "Could not save"))}\">")
            .Append("<div class=\"shell\">");

        html.Append(Navigation(page, logo));
        html.Append("<main class=\"content\">").Append(LoadingCard(Progress));
        AddPane(html, "resumen", OverviewTab.Build(page, logo));
        AddPane(html, "cofres", ChestsTab.Build(page));
        AddPane(html, "tesoros", TreasuresTab.Build(page));
        AddPane(html, "partidas", MatchesTab.Build(page));
        if (page.Teammates is { } mates)
        {
            AddPane(html, "companeros", Head("companeros", L.T("Compañeros", "Teammates"),
                L.Count(page.Matches.Count(m => m.Teammates is { Count: > 0 }), "partida en equipo", "partidas en equipo", "team match", "team matches")) + mates.Html);
        }
        AddPane(html, "ajustes", SettingsTab.Build(config));
        // "Saved" / "Could not save", for any tab (settings, struck-off prizes)
        html.Append("</main></div><div class=\"toast\" role=\"status\" aria-live=\"polite\" hidden></div><script>").Append(Script).Append("</script></body></html>");
        return html.ToString();
    }

    private static void AddPane(StringBuilder html, string id, string content) =>
        html.Append($"<div class=\"pane\" id=\"t-{id}\" role=\"tabpanel\" aria-labelledby=\"tab-{id}\">").Append(content).Append("</div>");

    /// <summary>Logo and the section buttons (a column on the left; a strip on top in narrow windows).</summary>
    private static string Navigation(PageData page, string? logo)
    {
        var sections = new List<(string Id, string Label, string? Count)>
        {
            ("resumen", L.T("Resumen", "Overview"), null),
            ("cofres", L.T("Cofres", "Chests"), null),
            ("tesoros", L.T("Tesoros", "Treasures"), page.TotalTreasures > 0 ? Num(page.TotalTreasures) : null),
            ("partidas", L.T("Partidas", "Matches"), Num(page.Matches.Count + page.Unjumped)),
        };
        if (page.Teammates is { } mates)
        {
            sections.Add(("companeros", L.T("Compañeros", "Teammates"), mates.Regulars > 0 ? Num(mates.Regulars) : null));
        }

        var nav = new StringBuilder("<aside class=\"nav-side\"><div class=\"brand\">");
        nav.Append(logo == null ? $"<span class=\"brand-mark\">{PageIcons.Chest}</span>" : $"<img src=\"{logo}\" alt=\"\" width=\"52\" height=\"52\">");
        nav.Append("<div class=\"brand-text\"><span>Fortnite</span><b>Chest Tracker</b></div></div>");
        nav.Append($"<nav class=\"tabs\" role=\"tablist\" aria-label=\"{L.T("Secciones de estadísticas", "Stats sections")}\" aria-orientation=\"vertical\">");
        foreach (var (id, label, count) in sections)
        {
            nav.Append(TabButton(id, label, "tab", count));
        }
        nav.Append(ClosedNotice());
        nav.Append(TabButton("ajustes", L.T("Configuración", "Settings"), "tab tab-last", null));
        return nav.Append("</nav></aside>").ToString();
    }

    /// <summary>Shown by the script under the last section when FCT stops answering (the page is no longer updated).</summary>
    private static string ClosedNotice() =>
        "<div class=\"down\" role=\"status\" hidden><span class=\"down-dot\" aria-hidden=\"true\"></span><div>"
        + $"<b>{Esc(L.F("{0} está cerrado", "{0} is closed", AppInfo.ShortName))}</b>"
        + $"<small>{Esc(L.T("Las cifras no se actualizan", "The figures are not updating"))}</small></div></div>";

    private static string TabButton(string id, string label, string classes, string? count)
    {
        var badge = count == null ? "" : $"<span class=\"tab-count\" aria-label=\"{Esc(L.F("{0} en total", "{0} in total", count))}\">{count}</span>";
        return $"<button class=\"{classes}\" role=\"tab\" type=\"button\" id=\"tab-{id}\" data-tab=\"{id}\" aria-controls=\"t-{id}\">{PageIcons.Tabs[id]}<span>{label}</span>{badge}</button>";
    }

    private static string LoadingCard(ScanProgress? progress)
    {
        if (progress is not { } p)
        {
            return "";
        }
        var percent = p.Total > 0 ? (int)Math.Round(100.0 * p.Done / p.Total) : 0;
        return "<div class=\"loader\"><div class=\"spinner\"></div><div class=\"loader-body\">"
               + $"<b>{L.F("Analizando tus replays… {0} / {1}", "Analyzing your replays… {0} / {1}", p.Done, p.Total)}</b>"
               + $"<div class=\"progress\"><i style=\"width:{percent}%\"></i></div>"
               + $"<span class=\"muted\">{L.T("Esta página se actualiza sola: las cifras de abajo aumentan a medida que se leen los replays.", "This page updates by itself: the numbers below grow as the replays are read.")}</span></div></div>";
    }
}

/// <summary>What several tabs share, computed once per page.</summary>
internal sealed class PageData
{
    public PageData(Config config, TrackerData data, int initialChests, int allMatches = 0)
    {
        Config = config;
        InitialChests = initialChests;
        Unjumped = data.UnjumpedMatches;
        AllMatches = Math.Max(allMatches, data.Matches.Count + data.UnjumpedMatches);
        Matches = data.Matches.OrderByDescending(LocalStart).ToList();
        PlayedDays = Matches.Select(LocalStart).Concat(data.UnjumpedStarts.Select(start => start.ToLocalTime()))
            .GroupBy(start => start.Date).ToDictionary(day => day.Key, day => day.Count());
        Treasures = Matches.SelectMany(m => m.TreasureFinds.Select(f => (m, f))).ToList();
        DetectedPrizes = Prizes.DetectedPrizeNames(Treasures);
        ManualTreasureMarks = Prizes.ManualMarksCount(config.PrizeMarks, DetectedPrizes);
        PirateToken = Treasures.Any(t => Prizes.ByName(Prizes.Describe(t.f, LocalStart(t.m)).Name) is { Type: Prizes.Kind.ShopAccess });
        ExtraKinds = config.ExtraKinds();
        Player = Matches.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";
        Teammates = config.ShowTeammates ? TeammatesTab.Build(Matches, ExtraKinds) : null;
    }

    /// <summary>Matches left without jumping from the bus: only in the matches played.</summary>
    public int Unjumped { get; }

    /// <summary>Matches played on each local day (those left in the bus included).</summary>
    public Dictionary<DateTime, int> PlayedDays { get; }

    /// <summary>Every match of the account: those before FCT (from its stats), then one more for each match played.</summary>
    public int AllMatches { get; }

    /// <summary>Every match played since FCT: the analyzed ones and those left in the bus.</summary>
    public int Played => Matches.Count + Unjumped;

    /// <summary>Matches played per day with at least one match.</summary>
    public double PerDay => PlayedDays.Count > 0 ? (double)Played / PlayedDays.Count : 0;

    public Config Config { get; }
    public int InitialChests { get; }

    /// <summary>Newest first.</summary>
    public List<MatchRecord> Matches { get; }

    public List<(MatchRecord m, TreasureFind f)> Treasures { get; }

    /// <summary>Names and IDs of treasures found in matches.</summary>
    public HashSet<string> DetectedPrizes { get; }

    /// <summary>Weekly treasures struck off by hand (not found in matches).</summary>
    public int ManualTreasureMarks { get; }

    /// <summary>Total treasures: found in matches plus those struck off by hand.</summary>
    public int TotalTreasures => Treasures.Count + ManualTreasureMarks;

    /// <summary>The player got the pirate token (the Pirate Shop Access) in one of the matches shown.</summary>
    public bool PirateToken { get; }
    public IReadOnlyCollection<string> ExtraKinds { get; }
    public string Player { get; }

    /// <summary>The teammates tab (null when teammates are turned off on the page).</summary>
    public (string Html, int Regulars)? Teammates { get; }
}
