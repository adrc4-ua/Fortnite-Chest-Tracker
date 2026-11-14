using System.Globalization;
using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>matches.csv: one line per match (oldest first), separated by ';' for spreadsheets.</summary>
internal static class MatchesCsv
{
    public static string Build(TrackerData data, Config config)
    {
        var csv = new StringBuilder();
        csv.AppendLine(L.T("fecha;modo;jugador;puesto;jugadores;cofres;cajas_municion;tesoros;archivo",
            "date;mode;player;placement;players;chests;ammo_boxes;treasures;file"));
        foreach (var match in data.Matches.Where(m => Profiles.IsEventMatch(m, config) && m.JumpedFromBus != false).OrderBy(LocalStart))
        {
            csv.AppendLine(string.Join(";",
                LocalStart(match).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Field(Pretty.Playlist(match.Playlist)),
                Field(match.PlayerName),
                match.Placement,
                match.TotalPlayers,
                match.ChestsOpened,
                match.AmmoBoxesOpened,
                Field(string.Join(" + ", match.TreasureFinds.Select(f => f.DisplayName))),
                Field(match.ReplayFile)));
        }
        return csv.ToString();
    }

    /// <summary>Text safe for one field: no separators or line breaks, and never read as a formula by a spreadsheet.</summary>
    private static string Field(string? value)
    {
        var text = (value ?? "").Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'');
        var start = text.TrimStart();
        return start.Length > 0 && start[0] is '=' or '+' or '-' or '@' or '\t' or '|' ? "'" + text : text;
    }
}
