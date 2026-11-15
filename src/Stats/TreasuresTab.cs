using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>
/// The "Tesoros" tab: what the treasure hunt offers now (and every week), where the player's treasures came from, each
/// treasure obtained with its moment and source, and, apart, the featured treasures opponents were seen getting.
/// </summary>
internal static class TreasuresTab
{
    public static string Build(PageData page)
    {
        var treasures = page.Treasures;
        var html = new StringBuilder(Head("tesoros", L.T("Tesoros", "Treasures"), L.T("Los que hay ahora y los que has conseguido", "The ones available now and the ones you got"),
            Figures(treasures, page.Matches.Count)));
        var (now, allWeeks) = Hunt(treasures, page.Config.PrizeMarks);
        html.Append("<div class=\"pair-row\">").Append(now).Append(Sources(treasures)).Append("</div>").Append(allWeeks);
        // the two lists keep their size, with or without treasures: a list scrolls inside, an empty one says so
        var own = treasures.Count > 0
            ? Table(treasures)
            : $"<div class=\"sheet list-void\"><p class=\"note\">{L.T("Aún no has encontrado tesoros. Aparecerán aquí con el momento y el cofre de origen.", "No treasures yet. They will show up here with the time and the chest they came from.")}</p></div>";
        html.Append(Section(L.T("Tesoros obtenidos por tu equipo", "Treasures obtained by your team"),
            L.Count(treasures.Count, "tesoro", "tesoros", "treasure", "treasures"), own, "fit tre-list"));
        return html.Append(Rivals(page.Matches)).ToString();
    }

    /// <summary>The tab's key numbers, small, at the right of its title.</summary>
    private static string Figures(List<(MatchRecord m, TreasureFind f)> treasures, int matchCount)
    {
        var featured = treasures.Count(t => t.f.Featured);
        var withTreasure = treasures.Select(t => t.m).Distinct().Count();
        var share = matchCount > 0 ? (100.0 * withTreasure / matchCount).ToString("0", L.Culture) : "0";
        static string Fig(string value, string text, bool gold = false) =>
            $"<span class=\"hfig{(gold ? " is-gold" : "")}\"><b>{value}</b>{text}</span>";
        return Fig(Num(treasures.Count), L.T("en total", "in total"), true)
               + Fig(Num(featured), L.T("destacados", "featured"))
               + Fig(Num(treasures.Count - featured), L.T("artilugios", "gizmos"))
               + Fig(share + " %", L.T("de partidas con tesoro", "of matches with one"));
    }

    /// <summary>
    /// Featured treasures opponents got near the player: only the golden chests opened close by are in the replay, so
    /// it is what was seen, not every one of the match. Kept apart, in no figure.
    /// </summary>
    private static string Rivals(List<MatchRecord> matches)
    {
        var seen = matches.SelectMany(m => (m.RivalTreasures ?? new List<RivalTreasure>()).Select(r => (m, r)))
            .OrderByDescending(x => LocalStart(x.m)).ThenByDescending(x => x.r.AtSeconds).ToList();
        var meta = $"{L.Count(seen.Count, "tesoro destacado", "tesoros destacados", "featured treasure", "featured treasures")} · "
                   + L.T("solo los abiertos cerca de ti", "only the ones opened near you");
        var title = L.T("Tesoros obtenidos por rivales", "Treasures obtained by opponents");
        if (seen.Count == 0)
        {
            return Section(title, meta,
                $"<div class=\"sheet list-void\"><p class=\"note\">{L.T("Ningún rival ha conseguido un tesoro destacado cerca de ti todavía.", "No opponent has got a featured treasure near you yet.")}</p></div>",
                "tre-list rivals-sec");
        }
        var html = new StringBuilder("<div class=\"sheet scrollx\"><table><thead><tr>")
            .Append($"<th>{L.T("Partida", "Match")}</th><th>{L.T("Tesoro", "Treasure")}</th><th class=\"ac\">{L.T("Rival", "Opponent")}</th>")
            .Append($"<th class=\"ac\">{L.T("Momento", "Time")}</th><th class=\"ac\">{L.T("Origen", "Source")}</th></tr></thead><tbody>");
        foreach (var (match, rival) in seen)
        {
            var who = rival.Player == null
                ? $"<span class=\"muted\">{L.T("Desconocido", "Unknown")}</span>"
                : Esc(rival.Player) + (rival.Bot ? $" <span class=\"chipl\">{L.T("bot", "bot")}</span>" : "");
            var find = new TreasureFind { PrizeId = rival.PrizeId, FromPrizeChest = true };
            html.Append("<tr>")
                .Append($"<td class=\"nowrap\">{L.DateTimeShort(LocalStart(match))}<div class=\"note\">{Esc(Pretty.Playlist(match.Playlist))}</div></td>")
                .Append($"<td>{TreasureCell(find, LocalStart(match))}</td><td class=\"ac nowrap\">{who}</td><td class=\"ac nowrap num\">{Pretty.Clock(rival.AtSeconds)}</td>")
                .Append($"<td class=\"ac\">{(rival.SourceKind.Length == 0 ? Dash : Esc(rival.SourceLabel))}</td></tr>");
        }
        return Section(title, meta, html.Append("</tbody></table></div>").ToString(), "tre-list rivals-sec");
    }

    private static string Sources(List<(MatchRecord m, TreasureFind f)> treasures)
    {
        // the chests that give treasures are always listed (also at 0); buried ones are normal chests
        var sources = new List<(string Name, int Count)>();
        foreach (var kind in new[] { ChestKinds.Normal, ChestKinds.Rare, ChestKinds.Extraction })
        {
            sources.Add((L.Name(kind), treasures.Count(t => ChestKinds.Shown(t.f.SourceKind) == kind)));
        }
        foreach (var other in treasures.Where(t => !sources.Exists(s => s.Name == L.Name(ChestKinds.Shown(t.f.SourceKind))))
                     .GroupBy(t => L.Name(ChestKinds.Shown(t.f.SourceKind))))
        {
            sources.Add((other.Key, other.Count()));
        }
        sources = sources.OrderByDescending(s => s.Count).ToList(); // stable: at a tie, the chests' order above
        var largest = Math.Max(1, sources.Max(s => s.Count));
        var rows = new StringBuilder();
        foreach (var (name, count) in sources)
        {
            rows.Append(Bar(Esc(name), Num(count), 100.0 * count / largest));
        }
        return Section(L.T("Dónde consigo mis tesoros", "Where I get my treasures"), L.Count(treasures.Count, "tesoro", "tesoros", "treasure", "treasures"),
            $"<div class=\"sheet sheet-pad\"><div class=\"bars\">{rows}</div></div>");
    }

    private static string Table(List<(MatchRecord m, TreasureFind f)> treasures)
    {
        var html = new StringBuilder("<div class=\"sheet scrollx\"><table><thead><tr>")
            .Append($"<th>{L.T("Partida", "Match")}</th><th>{L.T("Tesoro", "Treasure")}</th><th class=\"ac\">{L.T("Conseguido por", "Obtained by")}</th>")
            .Append($"<th class=\"ac\">{L.T("Momento", "Time")}</th><th class=\"ac\">{L.T("Origen", "Source")}</th></tr></thead><tbody>");
        foreach (var (match, find) in treasures)
        {
            var at = find.ObtainedAtSeconds is { } seconds
                ? find.TimeExact ? Pretty.Clock(seconds) : $"<span class=\"note\">{L.T("hacia", "around")}</span> {Pretty.Clock(seconds)}"
                : Dash;
            // Who opened the chest that paid out: the player, a teammate, or unknown (the squad got it out of sight).
            var finder = Esc(find.FinderLabel(match));
            if (find.Finder == TreasureFind.FinderUnknown)
            {
                finder = $"<span class=\"muted\">{finder}</span>";
            }
            html.Append("<tr>")
                .Append($"<td class=\"nowrap\">{L.DateTimeShort(LocalStart(match))}<div class=\"note\">{Esc(Pretty.Playlist(match.Playlist))}</div></td>")
                .Append($"<td>{TreasureCell(find, LocalStart(match))}</td><td class=\"ac nowrap\">{finder}</td><td class=\"ac nowrap num\">{at}</td>")
                .Append($"<td class=\"ac\">{Esc(find.SourceLabel)}</td></tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }

    /// <summary>Star, clean name, and its tags: "featured treasure" and the reward's kind.</summary>
    private static string TreasureCell(TreasureFind find, DateTime matchLocal)
    {
        var (name, type, featured) = Prizes.Describe(find, matchLocal);
        var tags = (featured && name != Prizes.Featured ? $"<span class=\"chipl chip-gold\">{Esc(Prizes.Featured)}</span>" : "")
                   + (type == null ? "" : $"<span class=\"chipl\">{Esc(type)}</span>");
        return $"<div class=\"tr-name\"><span class=\"star\">{(featured ? "★★" : "★")}</span><span>{Esc(name)}</span></div>"
               + (tags.Length > 0 ? $"<div class=\"tr-tags\">{tags}</div>" : "");
    }

    /// <summary>
    /// The hunt as it is now: this week's rewards as tiles (the ones obtained in gold), what any week can give, and a
    /// button that opens the five weeks (returned apart, as they span the whole width under the row).
    /// </summary>
    private static (string Now, string Weeks) Hunt(List<(MatchRecord m, TreasureFind f)> treasures, IReadOnlyDictionary<string, bool> marks)
    {
        var obtained = treasures.Select(t => Prizes.Describe(t.f, LocalStart(t.m)).Name)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name.Key, name => name.Count(), StringComparer.OrdinalIgnoreCase);
        int Got(Prizes.Prize prize) => obtained.GetValueOrDefault(Prizes.Shown(prize));
        var now = DateTime.Now;
        var current = Prizes.WeekOf(now);
        // local dates: a week changing at 3:00 in the morning ends on the day the next one starts
        string Dates(int week) => $"{ShortDay(Prizes.StartOf(week).Date)} – {ShortDay(Prizes.EndOf(week).AddTicks(-1).Date)}";
        var notYet = L.T("Premios aún no anunciados.", "Prizes not announced yet.");

        string Tile(Prizes.Prize prize)
        {
            var count = Got(prize);
            var under = count > 0 ? $"<span class=\"prize-won\">★ ×{count}</span>" : prize.Announced ? "" : $"<span class=\"chipl\">{L.T("sin confirmar", "unconfirmed")}</span>";
            return $"<div class=\"prize{(count > 0 ? " prize-got" : "")}\"><span class=\"prize-kind\"><i>{PageIcons.Of(prize)}</i><span>{Esc(Prizes.TypeName(prize.Type))}</span></span><b>{Esc(Prizes.Shown(prize))}</b>{under}</div>";
        }

        var meta = current > 0
            ? $"{L.F("Semana {0}", "Week {0}", current)} · {Dates(current)} · {L.F("quedan {0}", "{0} left", L.Count((int)Math.Ceiling((Prizes.EndOf(current) - now).TotalDays), "día", "días", "day", "days"))}"
            : L.T("Evento del 1 de noviembre al 5 de diciembre", "Event from November 1 to December 5");
        var card = new StringBuilder("<div class=\"sheet hunt\">");
        var thisWeek = current > 0 ? Prizes.All.Where(p => p.Week == current).ToList() : new List<Prizes.Prize>();
        if (thisWeek.Count > 0)
        {
            card.Append("<div class=\"prizes\">").Append(string.Concat(thisWeek.Select(Tile))).Append("</div>");
        }
        else if (current > 0)
        {
            card.Append($"<p class=\"note hunt-note\">{notYet}</p>");
        }
        card.Append($"<div class=\"everyweek\"><span class=\"everyweek-lbl\">{L.T("Todas las semanas", "Every week")}</span>");
        foreach (var prize in Prizes.All.Where(p => p.Week == 0))
        {
            // what any week can give, muted; only the shop access lights up once obtained (gizmos come all the time)
            var lit = prize.Type == Prizes.Kind.ShopAccess && Got(prize) > 0;
            card.Append($"<span class=\"token{(lit ? " token-got" : "")}\"><i>{PageIcons.Of(prize)}</i>{Esc(Prizes.Shown(prize))}</span>");
        }
        card.Append("</div></div>");
        // the button that opens the five weeks sits on the title's line, to keep the card short
        var weeksButton = $"<button class=\"weeks-btn\" type=\"button\" aria-expanded=\"false\" aria-controls=\"weeks\" data-open=\"{Esc(L.T("Ver todas las semanas", "See every week"))}\" data-close=\"{Esc(L.T("Ocultar las semanas", "Hide the weeks"))}\">{L.T("Ver todas las semanas", "See every week")}</button>";

        var weeks = new StringBuilder("<section class=\"weeks-sec\" id=\"weeks\" hidden><div class=\"weeks\">");
        for (var week = 1; week <= Prizes.WeekStarts.Length; week++)
        {
            var prizes = Prizes.All.Where(p => p.Week == week).ToList();
            var list = prizes.Count == 0
                ? $"<p class=\"note\">{notYet}</p>"
                : "<ul>" + string.Concat(prizes.Select(p =>
                {
                    // struck off once obtained, or by hand (the player's mark wins either way)
                    var count = Got(p);
                    var struck = marks.TryGetValue(p.Name, out var mark) ? mark : count > 0;
                    return $"<li class=\"mark{(struck ? " struck" : "")}\" data-prize=\"{Esc(p.Name)}\" role=\"button\" tabindex=\"0\" aria-pressed=\"{(struck ? "true" : "false")}\">"
                           + $"<i>{PageIcons.Of(p)}</i><span>{Esc(Prizes.Shown(p))}</span>{(count > 0 ? $"<b>×{count}</b>" : "")}</li>";
                })) + "</ul>";
            var isNow = week == current;
            weeks.Append($"<div class=\"week{(isNow ? " week-now" : "")}\"><div class=\"week-head\"><b>{L.F("Semana {0}", "Week {0}", week)}</b>")
                .Append(isNow ? $"<span class=\"chipl chip-gold\">{L.T("Esta semana", "This week")}</span>" : "")
                .Append($"<span class=\"week-dates\">{Dates(week)}</span></div>{list}</div>");
        }
        weeks.Append("</div></section>");
        return (Section(L.T("Tesoros disponibles actualmente", "Treasures available now"), meta, card.ToString(), headExtra: weeksButton), weeks.ToString());
    }
}
