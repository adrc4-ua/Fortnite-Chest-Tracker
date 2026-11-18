namespace FCT.Monitoring;

/// <summary>
/// The figures of the main window for the profile on screen, over its whole saved history (also matches whose replay
/// Fortnite deleted), with the match in progress included where it belongs. Same numbers as the stats page.
/// </summary>
internal static class ScreenFigures
{
    private static DateTime Day(MatchRecord match) => (match.MatchStartUtc ?? match.AnalyzedAtUtc).ToLocalTime().Date;

    /// <param name="matches">The profile's counted matches (<see cref="Profiles.View"/>).</param>
    /// <param name="live">The match in progress of this profile, or null.</param>
    /// <param name="initialChests">Chests before FCT.</param>
    /// <param name="earlierMatches">Matches before FCT.</param>
    /// <param name="manualAverage">Manual average entered by user, if any.</param>
    public static ChestStats Stats(List<MatchRecord> matches, MatchRecord? live, int initialChests = 0, int earlierMatches = 0, double? manualAverage = null)
    {
        var today = DateTime.Now.Date;
        var byDay = new Dictionary<DateTime, int>();
        foreach (var match in live == null ? matches : matches.Append(live))
        {
            var day = match == live ? today : Day(match);
            byDay[day] = byDay.GetValueOrDefault(day) + match.ChestsOpened;
        }
        var withLive = live == null ? matches : matches.Append(live).ToList();
        KeyValuePair<DateTime, int>? bestDay = byDay.Count == 0 ? null : byDay.MaxBy(d => d.Value);

        var totalMatches = matches.Count + earlierMatches;
        var totalChests = matches.Sum(m => m.ChestsOpened) + Math.Max(0, initialChests);
        double average;
        if (matches.Count == 0 && manualAverage is { } manual && manual > 0)
        {
            average = manual;
        }
        else if (totalMatches > 0)
        {
            average = (double)totalChests / totalMatches;
        }
        else if (manualAverage is { } m && m > 0)
        {
            average = m;
        }
        else
        {
            average = 0;
        }

        return new ChestStats(
            Matches: matches.Count,
            Average: average,
            BestMatch: withLive.Count == 0 ? 0 : withLive.Max(m => m.ChestsOpened),
            Treasures: withLive.Sum(m => m.TreasureFinds.Count),
            Today: byDay.GetValueOrDefault(today),
            BestDay: bestDay?.Key,
            BestDayChests: bestDay?.Value ?? 0)
        {
            LastDays = ThisWeek(matches, live, today),
            HistoryMatches = matches.Count,
            EarlierMatches = earlierMatches,
            InitialChests = initialChests,
            ManualAverage = manualAverage,
            HistorySince = matches.Count == 0 ? null : matches.Min(Day),
            LastMatch = Latest(matches) is { } last
                ? new LastMatchSummary(last.ChestsOpened, last.Chests.Count(c => c.Destroyed), last.TreasureFinds.Count, last.Placement, last.TotalPlayers, last.Eliminations, last.DurationSeconds)
                : null,
        };
    }

    /// <summary>The squad of the match in progress (if readable) or of the latest one: own chests and each teammate's.</summary>
    public static TeamSummary? Team(List<MatchRecord> matches, MatchRecord? live, IReadOnlyCollection<string> extraKinds)
    {
        var match = live ?? Latest(matches);
        if (match == null)
        {
            return null;
        }
        var mates = (match.Teammates ?? new List<TeammateRecord>())
            .Select(t => new TeamMember(t.Name, MatchRecord.Seen(t, extraKinds)))
            .OrderByDescending(t => t.Chests)
            .ToList();
        return new TeamSummary(live != null, match.OwnChests, mates, Unknown: match.Teammates == null);
    }

    private static MatchRecord? Latest(List<MatchRecord> matches) => matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();

    /// <summary>Monday to Sunday of the current week; days still to come are marked as future.</summary>
    private static List<DayChests> ThisWeek(List<MatchRecord> matches, MatchRecord? live, DateTime today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var week = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
        var chests = week.ToDictionary(d => d, _ => 0);
        foreach (var match in live == null ? matches : matches.Append(live))
        {
            var day = Day(match);
            if (chests.ContainsKey(day))
            {
                chests[day] += match.ChestsOpened;
            }
        }
        return week.Select(d => new DayChests(d, chests[d], Future: d > today)).ToList();
    }
}
