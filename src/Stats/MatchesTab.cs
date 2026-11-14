using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>The "Partidas" tab (the 100 newest matches) and the short table of the overview.</summary>
internal static class MatchesTab
{
    private const int Shown = 100;

    public static string Build(PageData page)
    {
        var matches = page.Matches;
        var rows = matches.Take(Shown).ToList();
        var subtitle = matches.Count > rows.Count
            ? L.F("las {0} más recientes de {1}", "latest {0} of {1}", rows.Count, Num(matches.Count))
            : L.Count(matches.Count, "partida", "partidas", "match", "matches");
        if (page.Unjumped > 0)
        {
            // played but left in the bus: counted as matches played only, not listed
            subtitle += " · " + L.F("y {0} sin saltar del autobús, que no cuentan", "and {0} without jumping from the bus, not counted",
                L.Count(page.Unjumped, "partida", "partidas", "match", "matches"));
        }
        return Head("partidas", L.T("Partidas", "Matches"), subtitle)
               + $"<section class=\"fit\">{Full(rows, page.Teammates != null, page.ExtraKinds)}</section>";
    }

    private static string NoMatches(int columns) =>
        $"<tr><td colspan=\"{columns}\" class=\"void\">{L.T("Aún no hay partidas analizadas. Juega una y aparecerá aquí.", "No matches analyzed yet. Play one and it will show up here.")}</td></tr>";

    private static string Place(MatchRecord match)
    {
        var tone = match.Placement switch { 1 => " pill-win", > 0 and <= 10 => " pill-top", _ => "" };
        var text = Esc(Pretty.Placement(match));
        return $"<span class=\"pill{tone}\">{(match.Placement == 1 ? "🏆 " + text : text)}</span>";
    }

    private static string Eliminations(MatchRecord match) => match.Eliminations is { } kills ? Num(kills) : Dash;

    private static string RowOpen(MatchRecord match) => match.Placement == 1 ? "<tr class=\"is-win\">" : "<tr>";

    /// <summary>The overview's latest matches: date, mode, place, eliminations, chests and treasures.</summary>
    public static string Short(List<MatchRecord> matches)
    {
        var html = new StringBuilder("<div class=\"sheet scrollx\"><table class=\"short\"><thead><tr>")
            .Append($"<th>{L.T("Fecha", "Date")}</th><th>{L.T("Modo", "Mode")}</th><th class=\"ac\">{L.T("Puesto", "Placement")}</th>")
            .Append($"<th class=\"ac\">{L.T("Elim.", "Elims")}</th><th class=\"ac\">{L.T("Duración", "Length")}</th>")
            .Append($"<th class=\"ac\">{L.T("Cofres", "Chests")}</th><th class=\"ac\">{L.T("Tesoros", "Treasures")}</th></tr></thead><tbody>");
        if (matches.Count == 0)
        {
            html.Append(NoMatches(7));
        }
        foreach (var match in matches)
        {
            var gems = match.TreasureFinds.Count > 0 ? $"<span class=\"gems\">{match.TreasureFinds.Count}{PageIcons.SmallGem}</span>" : Dash;
            html.Append(RowOpen(match))
                .Append($"<td class=\"nowrap\">{L.DateTimeShort(LocalStart(match))}</td><td class=\"note\">{Esc(Pretty.Playlist(match.Playlist))}</td>")
                .Append($"<td class=\"ac\">{Place(match)}</td><td class=\"ac\">{Eliminations(match)}</td>")
                .Append($"<td class=\"ac nowrap\">{Pretty.Minutes(match.DurationSeconds) ?? Dash}</td>")
                .Append($"<td class=\"ac\"><b>{Num(match.ChestsOpened)}</b></td><td class=\"ac\">{gems}</td></tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }

    /// <summary>
    /// The full table. With teammates on: the player's own chests, the team's (≥) and, under the mode, what each
    /// teammate was seen opening. Eliminations only when some match has them.
    /// </summary>
    private static string Full(List<MatchRecord> matches, bool withTeam, IReadOnlyCollection<string> kinds)
    {
        var withKills = matches.Any(m => m.Eliminations > 0);
        var html = new StringBuilder("<div class=\"sheet scrollx\"><table class=\"match-table\"><thead><tr>")
            .Append($"<th>{L.T("Fecha", "Date")}</th><th>{L.T("Modo", "Mode")}</th><th class=\"ac\">{L.T("Puesto", "Placement")}</th>")
            .Append(withKills ? $"<th class=\"ac\">{L.T("Elim.", "Elims")}</th>" : "")
            .Append($"<th class=\"ac\">{(withTeam ? L.T("Tú", "You") : L.T("Cofres", "Chests"))}</th>")
            .Append(withTeam ? $"<th class=\"ac\">{L.T("Equipo", "Team")}</th>" : "")
            .Append($"<th>{L.T("Tesoros", "Treasures")}</th></tr></thead><tbody>");
        if (matches.Count == 0)
        {
            html.Append(NoMatches(5 + (withKills ? 1 : 0) + (withTeam ? 1 : 0)));
        }
        var largest = Math.Max(1, matches.Count == 0 ? 0 : matches.Max(m => m.ChestsOpened));
        foreach (var match in matches)
        {
            var own = withTeam ? match.OwnChests : match.ChestsOpened;
            var squad = withTeam && match.Teammates is { Count: > 0 } mates ? mates : null;
            var mode = $"<span class=\"pill pill-mode\">{Esc(Pretty.Playlist(match.Playlist))}</span>";
            if (squad != null)
            {
                mode += "<div class=\"mates\">" + string.Concat(squad.OrderByDescending(t => MatchRecord.Seen(t, kinds))
                    .Select(t => $"<span class=\"mate\"><span class=\"mate-name\">{Esc(t.Name)}</span> <b>≥ {MatchRecord.Seen(t, kinds)}</b></span>")) + "</div>";
            }
            html.Append(RowOpen(match))
                .Append($"<td class=\"nowrap\">{L.DateTimeShort(LocalStart(match))}</td><td>{mode}</td><td class=\"ac\">{Place(match)}</td>")
                .Append(withKills ? $"<td class=\"ac\">{Eliminations(match)}</td>" : "")
                .Append($"<td class=\"ac\"><div class=\"count\"><span class=\"count-bar\"><i style=\"width:{Css(100.0 * own / largest)}\"></i></span><b>{own}</b></div></td>");
            if (withTeam)
            {
                html.Append(squad != null
                    ? $"<td class=\"ac nowrap\"><span class=\"note\">≥</span> <b class=\"accent\">{Num(match.OwnChests + match.TeammatesSeen(kinds))}</b></td>"
                    : $"<td class=\"ac\">{Dash}</td>");
            }
            html.Append($"<td>{Treasures(match)}</td></tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }

    private static string Treasures(MatchRecord match) =>
        match.TreasureFinds.Count == 0
            ? Dash
            : "<div class=\"stars\">" + string.Concat(match.TreasureFinds.Select(f =>
                $"<span class=\"star\">{(f.Featured ? "★★" : "★")} {Esc(Prizes.Describe(f, LocalStart(match)).Name)}</span>")) + "</div>";
}
