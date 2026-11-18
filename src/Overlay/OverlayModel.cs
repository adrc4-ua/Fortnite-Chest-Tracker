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
    /// <param name="estimation">Progress of an active estimation waiting for played matches ("2/5", or null).</param>
    /// <param name="baseAverage">Base manual average, displayed when today has no matches yet.</param>
    public static OverlayView Build(TrackerData data, int initialChests = 0, string? estimation = null, double? baseAverage = null)
    {
        var total = data.Matches.Sum(m => m.ChestsOpened) + Math.Max(0, initialChests);
        var latest = data.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();

        var rows = new List<OverlayRow>();
        rows.AddRange(TodayRows(data, baseAverage));
        var treasures = data.Matches.Sum(m => m.TreasureFinds.Count);
        rows.Add(new OverlayRow(L.T("Tesoros", "Treasures"), $"{TreasureIcon} {L.N(treasures)}", Treasure: true));

        var shownTotal = L.N(total);
        return new OverlayView(shownTotal, latest == null ? null : L.N(latest.ChestsOpened), LastMatchCaption, rows, estimation);
    }

    /// <summary>Builds the overlay view including estimation progress from config if active.</summary>
    public static OverlayView Build(TrackerData data, Config config, string? profileKey = null)
    {
        var key = profileKey ?? Profiles.Current(data, config)?.Key;
        var initial = Profiles.InitialChests(config, key);
        var estimation = EstimationProgress(data, config, profileKey);
        var estimate = (!string.IsNullOrEmpty(key) ? config.Estimates.GetValueOrDefault(key) : null)
                       ?? config.Estimates.GetValueOrDefault("")
                       ?? config.Estimates.Values.FirstOrDefault(e => e.ManualAverage != null);
        return Build(data, initial, estimation, estimate?.ManualAverage);
    }

    /// <summary>
    /// Progress of an active estimation waiting for played matches ("2/5", or null if none is pending).
    /// </summary>
    public static string? EstimationProgress(TrackerData view, Config config, string? profileKey = null)
    {
        var key = profileKey ?? Profiles.Current(view, config)?.Key;
        var estimate = config.Estimates.GetValueOrDefault(key ?? "") ?? config.Estimates.GetValueOrDefault("");
        if (estimate is { Done: false, ManualAverage: null })
        {
            var result = HistoryEstimator.Compute(view.Matches, estimate.TotalMatches, estimate.SnapshotUtc, estimate.SampleSize, estimate.ManualAverage);
            if (result.Chests == null && result.Waiting > 0)
            {
                var target = result.NewMatches + result.Waiting;
                return $"{result.NewMatches}/{target}";
            }
        }
        return null;
    }

    /// <summary>
    /// "Hoy" (today's chests) and "Media" (per finished match today, or base manual average when today has none). Values are bare
    /// numbers so columns never collide; the window also shows "n partidas · media x" under today's figure.
    /// </summary>
    private static IEnumerable<OverlayRow> TodayRows(TrackerData data, double? baseAverage = null)
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
        else if (baseAverage is { } baseAvg && baseAvg > 0)
        {
            average = Math.Round(baseAvg, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
        }
        yield return new OverlayRow(L.T("Hoy", "Today"), L.N(chests), Detail: detail);
        yield return new OverlayRow(L.T("Media", "Avg."), average);
    }
}
