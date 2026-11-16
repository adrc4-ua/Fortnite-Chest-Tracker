namespace FCT.Overlay;

/// <summary>
/// Builds the <see cref="OverlayView"/> shared by the main window, the game overlay and the OBS page, from the profile's
/// matches.
/// </summary>
public static class OverlayModel
{
    /// <summary>Caption of the second number between matches (also the width the overlay reserves for it).</summary>
    public static string LastMatchCaption => L.T("anterior", "previous");

    public const string TreasureIcon = "💎";

    /// <param name="data">The profile's counted matches (see <see cref="Profiles.View"/>).</param>
    /// <param name="initialChests">Chests before FCT: they only add to the total.</param>
    public static OverlayView Build(TrackerData data, int initialChests = 0)
    {
        var total = data.Matches.Sum(m => m.ChestsOpened) + Math.Max(0, initialChests);
        var latest = data.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();

        var rows = new List<OverlayRow>();
        rows.AddRange(TodayRows(data));
        var treasures = data.Matches.Sum(m => m.TreasureFinds.Count);
        rows.Add(new OverlayRow(L.T("Tesoros", "Treasures"), $"{TreasureIcon} {L.N(treasures)}", Treasure: true));

        var shownTotal = L.N(total);
        return new OverlayView(shownTotal, latest == null ? null : L.N(latest.ChestsOpened), LastMatchCaption, rows);
    }

    /// <summary>
    /// "Hoy" (today's chests) and "Media" (per finished match today). Values are bare
    /// numbers so columns never collide; the window also shows "n partidas · media x" under today's figure.
    /// </summary>
    private static IEnumerable<OverlayRow> TodayRows(TrackerData data)
    {
        var today = DateTime.Now.Date;
        var finished = data.Matches.Where(m => (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime().Date == today).ToList();
        var chests = finished.Sum(m => m.ChestsOpened);

        var average = "—";
        string? detail = null;
        if (finished.Count > 0)
        {
            average = Math.Round((double)chests / finished.Count, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
            detail = $"{L.Count(finished.Count, "partida", "partidas", "match", "matches")} · {L.T("media", "avg.")} {average}";
        }
        yield return new OverlayRow(L.T("Hoy", "Today"), L.N(chests), Detail: detail);
        yield return new OverlayRow(L.T("Media", "Avg."), average);
    }
}
