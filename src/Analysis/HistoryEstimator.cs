namespace FCT.Analysis;

/// <summary>
/// Estimate of the chests opened in the matches FCT has no replay of ("Cofres antes de FCT"):
/// <c>average × (total matches − replays already counted)</c>.
/// <list type="bullet">
/// <item>The average mixes the replays already counted (matches from before the total was taken) with the next
/// matches the player plays from then on, as many as they chose (5 by default, 10 recommended: the more, the closer).
/// It is the average over all of them together, so each replay weighs as much as a new match: the more replays there
/// are, the more they count in it. Without replays it only comes from the new matches.</item>
/// <item>The new matches are not in the total (it was taken before them), so they are not subtracted.</item>
/// <item>The total is the current season's (Fortnite stats, season window), so only the replays of that season are
/// subtracted: the season is the Fortnite major version of the newest replay (each season is a major version). Replays
/// with no readable version count as this season's. Replays of earlier seasons still go into the average.</item>
/// <item>The average is rounded down when there was no replay at all, or <see cref="ReplayCap"/> or more (Fortnite
/// keeps no more: the history is cut); otherwise it is used as is.</item>
/// <item>Or the player types the average: it is used as is, with no waiting and no rounding.</item>
/// </list>
/// </summary>
public static class HistoryEstimator
{
    /// <summary>Saved matches from which the average is computed directly without waiting for sample matches.</summary>
    public const int AutoAverageMatches = 15;

    /// <summary>Replays Fortnite keeps at most on PC (not documented by Epic: checked by hand).</summary>
    public const int ReplayCap = 100;

    /// <summary>New matches the average needs by default, and the recommended number.</summary>
    public const int DefaultSample = 5, RecommendedSample = 10;

    /// <summary>Choices offered for the number of new matches.</summary>
    public static readonly int[] SampleChoices = { 5, 10, 15, 20 };

    /// <param name="Chests">Estimated chests, or null while waiting for new matches.</param>
    /// <param name="Replays">Replays already counted (matches from before the total was taken), of every season.</param>
    /// <param name="SeasonReplays">Of those, the ones of the current season: the only ones subtracted from the total.</param>
    /// <param name="NewMatches">New matches used so far (at most the chosen number).</param>
    /// <param name="Average">Chests per match used (or, while waiting, the replays' one so far).</param>
    /// <param name="RoundedDown">The average is rounded down (no replays, or at the replay limit).</param>
    /// <param name="Waiting">New matches still to play before it is calculated.</param>
    /// <param name="Provisional">While waiting: the estimate with the replays' average alone (null without replays).</param>
    public sealed record Result(int? Chests, int Replays, int SeasonReplays, int NewMatches, double? Average, bool RoundedDown, int Waiting, int? Provisional)
    {
        /// <summary>The season's matches FCT has no replay of (total − this season's replays): the ones before FCT.</summary>
        public int Missing { get; init; }
    }

    /// <param name="matches">Matches of the profile, as counted (own chests, with the chosen extra kinds).</param>
    /// <param name="totalMatches">The player's matches this season according to the stats.</param>
    /// <param name="snapshotUtc">When that total was taken: matches before it are the replays, after it the new ones.</param>
    /// <param name="sample">New matches to play for the average.</param>
    /// <param name="manualAverage">Average typed by the player (chests per match), or null to calculate it.</param>
    public static Result Compute(IReadOnlyList<MatchRecord> matches, int totalMatches, DateTime snapshotUtc, int sample = DefaultSample, double? manualAverage = null)
    {
        static DateTime Start(MatchRecord m) => m.MatchStartUtc ?? m.AnalyzedAtUtc;
        sample = Math.Max(0, sample);
        var replays = matches.Where(m => Start(m) <= snapshotUtc).ToList();
        var season = replays.OrderByDescending(Start).Select(m => Profiles.ParseFortniteMajorVersion(m.GameVersion)).FirstOrDefault(v => v != null);
        var seasonReplays = replays.Count(m => season == null || Profiles.ParseFortniteMajorVersion(m.GameVersion) is not int v || v == season);
        var missing = Math.Max(0, totalMatches - seasonReplays);
        int Times(double average) => (int)Math.Round(average * missing, MidpointRounding.AwayFromZero);

        if (manualAverage is double typed && typed >= 0)
        {
            return new Result(Times(typed), replays.Count, seasonReplays, 0, typed, false, 0, null) { Missing = missing };
        }

        var rounded = replays.Count == 0 || replays.Count >= ReplayCap;
        double Round(double a) => rounded ? Math.Floor(a) : a;
        if (sample == 0)
        {
            var autoAvg = replays.Count > 0 ? Round((double)replays.Sum(m => m.OwnChests) / replays.Count) : 0;
            return new Result(Times(autoAvg), replays.Count, seasonReplays, 0, autoAvg, rounded, 0, null) { Missing = missing };
        }
        var fresh = matches.Where(m => Start(m) > snapshotUtc).OrderBy(Start).Take(sample).ToList();
        if (fresh.Count < sample)
        {
            double? sofar = replays.Count > 0 ? Round((double)replays.Sum(m => m.OwnChests) / replays.Count) : null;
            return new Result(null, replays.Count, seasonReplays, fresh.Count, sofar, rounded, sample - fresh.Count, sofar is double s ? Times(s) : null) { Missing = missing };
        }
        var all = replays.Concat(fresh).ToList();
        var average = Round((double)all.Sum(m => m.OwnChests) / all.Count);
        return new Result(Times(average), replays.Count, seasonReplays, fresh.Count, average, rounded, 0, null) { Missing = missing };
    }
}
