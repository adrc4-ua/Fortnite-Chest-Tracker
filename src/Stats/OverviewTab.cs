using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>The "Resumen" tab: headline figures, the 30-day curve, latest matches and treasures, and the team strip.</summary>
internal static class OverviewTab
{
    public static string Build(PageData page, string? logo)
    {
        var matches = page.Matches;
        var counted = matches.Sum(m => m.ChestsOpened);
        var analyzed = L.Count(matches.Count + page.Unjumped, "partida analizada", "partidas analizadas", "match analyzed", "matches analyzed");
        var html = new StringBuilder(Head("resumen", L.T("Resumen", "Overview"), page.Player.Length > 0 ? $"{Esc(page.Player)} · {analyzed}" : analyzed));
        html.Append(Figures(page, counted, logo));
        html.Append(Curve(matches));

        html.Append("<div class=\"pair\"><section class=\"fit\">");
        var seeAll = matches.Count > 5 ? $"<a class=\"more-link\" href=\"#partidas\" data-go=\"partidas\">{L.T("Ver todas", "See all")} →</a>" : "";
        html.Append($"<div class=\"sec-head\"><h2>{L.T("Últimas partidas", "Latest matches")}</h2>{seeAll}</div>");
        html.Append(MatchesTab.Short(matches.Take(5).ToList()));
        html.Append("</section><div class=\"pile\">").Append(LatestTreasures(page.Treasures));
        if (page.Teammates != null)
        {
            html.Append(TeamStrip(matches, page.ExtraKinds));
        }
        return html.Append("</div></div>").ToString();
    }

    private static string Figures(PageData page, int counted, string? logo)
    {
        var matches = page.Matches;
        var today = DateTime.Now.Date;
        var todays = matches.Where(m => LocalStart(m).Date == today).ToList();
        var wins = matches.Count(m => m.Placement == 1);
        var best = matches.Count == 0 ? 0 : matches.Max(m => m.ChestsOpened);
        var bestDay = matches.GroupBy(m => LocalStart(m).Date).Select(day => (Day: day.Key, Chests: day.Sum(m => m.ChestsOpened)))
            .OrderByDescending(day => day.Chests).FirstOrDefault();

        var notes = new List<string>();
        if (page.InitialChests > 0)
        {
            notes.Add(L.F("Incluye {0} estimados de antes de usar la herramienta", "Includes {0} estimated from before the tool", Num(page.InitialChests)));
        }
        var fromMates = matches.Sum(m => m.TeammatesAdded);
        if (page.Config.CountTeammateChests && fromMates > 0)
        {
            notes.Add(L.F("incluye ≥ {0} de tus compañeros", "includes ≥ {0} from your teammates", Num(fromMates)));
        }
        var todayLine = $"{L.T("Hoy", "Today")}: <b>{Num(todays.Sum(m => m.ChestsOpened))}</b>"
                        + (todays.Count > 0 ? " · " + L.Count(todays.Count, "partida", "partidas", "match", "matches") : "");

        var hero = "<div class=\"fig fig-hero\">" + (logo == null ? "" : $"<img src=\"{logo}\" alt=\"\" width=\"88\" height=\"88\">")
                   + $"<div><div class=\"fig-label\">{L.T("Cofres abiertos", "Chests opened")}</div><div class=\"fig-value\">{Num(counted + Math.Max(0, page.InitialChests))}</div>"
                   + $"<div class=\"fig-note fig-today\">{todayLine}</div>"
                   + (notes.Count > 0 ? $"<div class=\"fig-note\">{string.Join(" · ", notes)}</div>" : "") + "</div></div>";
        var winShare = matches.Count > 0 ? 100.0 * wins / matches.Count : 0;
        return "<div class=\"figures\">" + hero
               + Figure("wins", L.T("Victorias", "Wins"), Num(wins), wins > 0 ? L.F("{0} % de tus partidas", "{0} % of your matches", winShare.ToString("0", L.Culture)) : "—")
               + Figure("matches", L.T("Partidas", "Matches"), Num(matches.Count + page.Unjumped),
                   page.Unjumped > 0 ? L.F("{0} sin saltar del autobús", "{0} without jumping", page.Unjumped) : L.T("analizadas", "analyzed"))
               + Figure("avg", L.T("Media", "Average"), (matches.Count > 0 ? (double)counted / matches.Count : 0).ToString("0.0", L.Culture), L.T("cofres por partida", "chests per match"))
               + Figure("best", L.T("Récord", "Best match"), Num(best), L.T("cofres en una partida", "chests in one match"))
               + Figure("day", L.T("Mejor día", "Best day"), bestDay.Chests > 0 ? ShortDay(bestDay.Day) : "—", bestDay.Chests > 0 ? L.Count(bestDay.Chests, "cofre", "cofres", "chest", "chests") : "—")
               + "</div>";
    }

    private static string Figure(string icon, string label, string value, string note) =>
        $"<div class=\"fig\"><span class=\"fig-icon\">{PageIcons.Figures[icon]}</span><div class=\"fig-label\">{label}</div><div class=\"fig-value\">{value}</div><div class=\"fig-note\">{note}</div></div>";

    private static string Curve(List<MatchRecord> matches)
    {
        var today = DateTime.Now.Date;
        var days = Enumerable.Range(-29, 30).Select(offset => today.AddDays(offset)).ToList();
        var chests = days.Select(day => matches.Where(m => LocalStart(m).Date == day).Sum(m => m.ChestsOpened)).ToList();
        var played = chests.Where(c => c > 0).ToList();
        var meta = $"{L.T("Últimos 30 días", "Last 30 days")} · {L.Count(chests.Sum(), "cofre", "cofres", "chest", "chests")} · {L.Count(played.Count, "día jugado", "días jugados", "day played", "days played")}";
        if (played.Count > 0)
        {
            // the dashed line's legend, out of the chart so it never sits on the curve
            meta += $" · <span class=\"c-key\" aria-hidden=\"true\"></span>{L.T("media", "average")} <b>{played.Average().ToString("0", L.Culture)}</b> {L.T("por día jugado", "per day played")}";
        }
        return Section(L.T("Evolución de cofres", "Chests over time"), meta, $"<div class=\"sheet sheet-pad\">{ChestCurve.Build(days, chests)}</div>");
    }

    /// <summary>The eight newest treasures: icon, name, match and kind.</summary>
    private static string LatestTreasures(List<(MatchRecord m, TreasureFind f)> treasures)
    {
        var seeAll = treasures.Count > 0 ? $"<a class=\"more-link\" href=\"#tesoros\" data-go=\"tesoros\">{L.F("Ver los {0}", "See all {0}", Num(treasures.Count))} →</a>" : "";
        var html = new StringBuilder($"<section class=\"stretch\"><div class=\"sec-head\"><h2>{L.T("Últimos tesoros", "Latest treasures")}</h2>{seeAll}</div><div class=\"sheet list-sheet\">");
        if (treasures.Count == 0)
        {
            return html.Append($"<p class=\"note list-empty\">{L.T("Aún no has encontrado tesoros.", "No treasures yet.")}</p></div></section>").ToString();
        }
        html.Append("<ul class=\"finds\">");
        foreach (var (match, find) in treasures.OrderByDescending(t => LocalStart(t.m)).ThenByDescending(t => t.f.ObtainedAtSeconds ?? -1).Take(8))
        {
            var (name, type, featured) = Prizes.Describe(find, LocalStart(match));
            var kind = featured ? (type == null ? Prizes.Featured : $"{Prizes.Featured} · {type}") : type ?? L.T("Artilugio", "Gizmo");
            html.Append($"<li><span class=\"item-ico{(featured ? " is-featured" : "")}\">{PageIcons.Item(name)}</span>")
                .Append($"<span class=\"item-txt\"><b>{Esc(name)}</b><small>{L.DateTimeShort(LocalStart(match))} · {Esc(kind)}</small></span></li>");
        }
        return html.Append("</ul></div></section>").ToString();
    }

    /// <summary>A link to the teammates tab: how much of the team's chests they open, and who you play with most.</summary>
    private static string TeamStrip(List<MatchRecord> matches, IReadOnlyCollection<string> kinds)
    {
        var squadMatches = matches.Where(m => m.Teammates is { Count: > 0 }).ToList();
        if (squadMatches.Count == 0)
        {
            return "";
        }
        var own = squadMatches.Sum(m => m.OwnChests);
        var theirs = squadMatches.Sum(m => m.TeammatesSeen(kinds));
        var share = own + theirs > 0 ? 100.0 * theirs / (own + theirs) : 0;
        var regular = squadMatches.SelectMany(m => m.Teammates!)
            .GroupBy(t => t.EpicId ?? t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(mate => (Name: mate.Last().Name, Matches: mate.Count()))
            .OrderByDescending(mate => mate.Matches)
            .First();
        return $"<section class=\"teamlink-sec\"><a class=\"sheet teamlink\" href=\"#companeros\" data-go=\"companeros\" aria-label=\"{L.T("Ver compañeros", "See teammates")}\">"
               + $"<div><span class=\"note\">{L.T("Cofres del equipo abiertos por tus compañeros", "Team chests opened by your teammates")}</span><b>≥ {share.ToString("0", L.Culture)}&nbsp;%</b><span class=\"meter\"><i style=\"width:{share.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%\"></i></span></div>"
               + $"<div><span class=\"note\">{L.T("Con quién más juegas", "Who you play with most")}</span><b class=\"name\">{Esc(regular.Name)}</b><span class=\"note\">{L.Count(regular.Matches, "partida juntos", "partidas juntos", "match together", "matches together")}</span></div>"
               + "<span class=\"teamlink-go\" aria-hidden=\"true\">→</span></a></section>";
    }
}
