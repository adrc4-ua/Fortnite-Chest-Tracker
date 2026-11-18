using System.Text.RegularExpressions;

namespace FCT.Analysis;

/// <summary>One Epic account that has matches on this PC, with its counted totals.</summary>
public sealed record Profile(string Key, string Name, int Matches, int Chests, DateTime LastPlayed);

/// <summary>
/// Several people can play on one PC: matches are grouped by account ("profile") and only the chosen profile is shown.
/// This is also where "which matches count" is decided (season, "count from" date) and how a match is adjusted to the
/// player's settings, so every screen agrees on the same numbers.
/// </summary>
public static class Profiles
{
    /// <summary>Oldest Fortnite version counted: v43 (Chapter 7 mini season).</summary>
    public const int MinFortniteMajorVersion = 43;

    /// <summary>v43 came out in November 2026: matches without a readable version are judged by their date.</summary>
    private static readonly DateTime FirstCountedDayUtc = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Regex VersionNumber = new(@"(?:Release-|v|\b)(?<major>\d+)\.\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ---- Identity and time ---------------------------------------------------------------------------------------------

    /// <summary>The account a match belongs to: its Epic id (stable across renames), or else the player name.</summary>
    public static string Key(MatchRecord match) => string.IsNullOrEmpty(match.EpicId) ? match.PlayerName ?? "" : match.EpicId;

    private static DateTime PlayedUtc(MatchRecord match) => match.MatchStartUtc ?? match.AnalyzedAtUtc;

    /// <summary>The local calendar day the match was played on.</summary>
    public static DateTime Day(MatchRecord match) => PlayedUtc(match).ToLocalTime().Date;

    // ---- Which matches count -----------------------------------------------------------------------------------------------

    /// <summary>Major version from Fortnite's build string ("++Fortnite+Release-43.00" → 43), or null.</summary>
    public static int? ParseFortniteMajorVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }
        var found = VersionNumber.Match(version);
        return found.Success && int.TryParse(found.Groups["major"].ValueSpan, out var major) ? major : null;
    }

    /// <summary>The match is from v43 or later (or, with no readable version, from November 2026 on).</summary>
    public static bool IsSupportedVersion(MatchRecord? match)
    {
        if (match == null)
        {
            return false;
        }
        if (ParseFortniteMajorVersion(match.GameVersion) is { } major)
        {
            return major >= MinFortniteMajorVersion;
        }
        var played = PlayedUtc(match);
        return played == default || played >= FirstCountedDayUtc;
    }

    /// <summary>Counts for the treasure hunt: a supported version and one of its modes (<see cref="EventModes"/>; Ranked if set).</summary>
    public static bool IsEventMatch(MatchRecord? match, Config? config = null) =>
        IsSupportedVersion(match) && EventModes.Counts(match!.Playlist, config?.CountRanked ?? false);

    /// <summary>A match played: of the hunt, not before the "count from" date of the settings (with or without the jump).</summary>
    public static bool Played(MatchRecord match, Config config) =>
        IsEventMatch(match, config) && (config.CountFrom == null || Day(match) >= config.CountFrom.Value.Date);

    /// <summary>Counts for the figures: a match played in which the player left the battle bus.</summary>
    public static bool Counted(MatchRecord match, Config config) => Played(match, config) && match.JumpedFromBus != false;

    /// <summary>The match as the settings count it (extra kinds, teammates' chests); see <see cref="MatchRecord.WithExtras"/>.</summary>
    public static MatchRecord? Adjust(MatchRecord? match, Config config) =>
        match?.WithExtras(config.ExtraKinds(), config.CountTeammateChests);

    // ---- Profiles ------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Every account with counted matches, most recently played first. Without settings, only the version filter
    /// applies and totals are the raw ones.
    /// </summary>
    public static List<Profile> List(TrackerData data, Config? config = null)
    {
        var profiles = new List<Profile>();
        foreach (var account in data.Matches.GroupBy(Key))
        {
            var counted = account.Where(m => config == null ? IsEventMatch(m) : Counted(m, config)).ToList();
            if (counted.Count == 0)
            {
                continue;
            }
            var newestFirst = counted.OrderByDescending(PlayedUtc).ToList();
            var name = newestFirst.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                       ?? L.T("Cuenta desconocida", "Unknown account");
            var chests = counted.Sum(m => (config == null ? m : Adjust(m, config)!).ChestsOpened);
            profiles.Add(new Profile(account.Key, name, counted.Count, chests, PlayedUtc(newestFirst[0])));
        }
        if (profiles.Count == 0 && config != null)
        {
            var estimate = config.Estimates.Values.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.PlayerName));
            if (estimate != null)
            {
                var key = !string.IsNullOrWhiteSpace(config.Profile) ? config.Profile
                    : !string.IsNullOrWhiteSpace(estimate.ProfileKey) ? estimate.ProfileKey
                    : estimate.PlayerName!;
                profiles.Add(new Profile(key, estimate.PlayerName!, 0, 0, DateTime.UtcNow));
            }
        }
        return profiles.OrderByDescending(p => p.LastPlayed).ToList();
    }

    /// <summary>The profile chosen in the settings, or the most recently played one; null before any match.</summary>
    public static Profile? Current(TrackerData data, Config config)
    {
        var profiles = List(data, config);
        return profiles.Find(p => p.Key == config.Profile) ?? profiles.FirstOrDefault();
    }

    /// <summary>The counted, adjusted matches of one account.</summary>
    public static List<MatchRecord> MatchesOf(TrackerData data, Config config, string key) =>
        data.Matches.Where(m => Key(m) == key && Counted(m, config)).Select(m => Adjust(m, config)!).ToList();

    /// <summary>
    /// The history as every screen sees it: only the current profile's counted matches, adjusted to the settings.
    /// A view, not a copy to save (it shares the processed-files table with the real history).
    /// </summary>
    public static TrackerData View(TrackerData data, Config config)
    {
        var shown = Current(data, config)?.Key;
        return new TrackerData
        {
            Version = data.Version,
            AnalyzerVersion = data.AnalyzerVersion,
            CreatedUtc = data.CreatedUtc,
            ProcessedFiles = data.ProcessedFiles,
            Matches = data.Matches.Where(m => (shown == null || Key(m) == shown) && Counted(m, config)).Select(m => Adjust(m, config)!).ToList(),
            UnjumpedStarts = data.Matches.Where(m => (shown == null || Key(m) == shown) && Played(m, config) && m.JumpedFromBus == false)
                .Select(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).ToList(),
        };
    }

    // ---- Chests before FCT -------------------------------------------------------------------------------------------------------

    /// <summary>Estimated chests before FCT of an account ("" or null: before the first match). Never negative.</summary>
    public static int InitialChests(Config config, string? key)
    {
        if (config.InitialChests == null)
        {
            return 0;
        }
        if (config.InitialChests.TryGetValue(key ?? "", out var chests) && chests > 0)
        {
            return chests;
        }
        if (config.InitialChests.TryGetValue("", out chests) && chests > 0)
        {
            return chests;
        }
        return 0;
    }

    public static int InitialChests(TrackerData data, Config config) => InitialChests(config, Current(data, config)?.Key);

    /// <summary>
    /// Matches before FCT of an account: those of its season total (from the stats or typed when the estimate was
    /// chosen) that FCT has no replay of; 0 without an estimate. <paramref name="matches"/>: its counted matches.
    /// </summary>
    public static int EarlierMatches(Config config, string? key, IReadOnlyList<MatchRecord> matches)
    {
        // asked before the first match: still filed under "" until its average can be worked out
        var estimate = config.Estimates.GetValueOrDefault(key ?? "") ?? config.Estimates.GetValueOrDefault("");
        return estimate is { Done: true, TotalMatches: > 0 }
            ? HistoryEstimator.Compute(matches, estimate.TotalMatches, estimate.SnapshotUtc, estimate.SampleSize, estimate.ManualAverage).Missing
            : 0;
    }

    /// <summary>Every match of the account on screen: those before FCT, the counted ones and those left in the bus.</summary>
    public static int AllMatches(TrackerData data, Config config, TrackerData view) =>
        EarlierMatches(config, Current(data, config)?.Key, view.Matches) + view.Matches.Count + view.UnjumpedMatches;

    /// <summary>"Name  (12 partidas, 240 cofres)" for the profile selector.</summary>
    public static string Describe(Profile profile) =>
        $"{profile.Name}  ({L.Count(profile.Matches, "partida", "partidas", "match", "matches")}, {L.Count(profile.Chests, "cofre", "cofres", "chest", "chests")})";
}
