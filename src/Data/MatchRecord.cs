using System.Text.Json.Serialization;

namespace FCT.Data;

/// <summary>
/// Everything FCT keeps about one Battle Royale match, as stored in data.json. Property names are the file format:
/// renaming one would make saved histories unreadable.
/// </summary>
public sealed class MatchRecord
{
    // ---- Identity of the replay ------------------------------------------------------------------------------------

    public string SessionId { get; set; } = "";
    public string ReplayFile { get; set; } = "";
    public DateTime AnalyzedAtUtc { get; set; }
    public int AnalyzerVersion { get; set; } = 1;

    // ---- The match --------------------------------------------------------------------------------------------------

    public DateTime? MatchStartUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Playlist { get; set; }
    public string? GameVersion { get; set; }
    public bool IsBattleRoyale { get; set; } = true;

    /// <summary>
    /// The player left the battle bus. A match without the jump only counts as a match played, in no other figure. Null
    /// for matches read before this was known (they count as before).
    /// </summary>
    public bool? JumpedFromBus { get; set; }
    public int? Placement { get; set; }
    public int? TotalPlayers { get; set; }
    public int? Eliminations { get; set; }

    // ---- Whose match it is --------------------------------------------------------------------------------------------

    public string? PlayerName { get; set; }
    public string? EpicId { get; set; }
    public bool TrackedByUsername { get; set; }
    public bool IsReplayOwner { get; set; }

    /// <summary>Owner of the replay when none of the configured players took part (the match is then not counted).</summary>
    public string? NotTrackedOwner { get; set; }

    // ---- What was opened -------------------------------------------------------------------------------------------------

    public int ChestsOpened { get; set; }
    public int AmmoBoxesOpened { get; set; }
    public int OtherContainersOpened { get; set; }

    /// <summary>Searches that are not chests, per kind (see <see cref="ChestKinds"/>).</summary>
    public Dictionary<string, int> OtherInteractions { get; set; } = new();

    public List<ChestRecord> Chests { get; set; } = new();
    public List<TreasureFind> TreasureFinds { get; set; } = new();

    /// <summary>True once the end-of-match treasure screen named the gizmos of this match.</summary>
    public bool TreasureScreenRead { get; set; }

    /// <summary>Squad mates (empty when playing alone; null in matches read by analyzers older than version 13).</summary>
    public List<TeammateRecord>? Teammates { get; set; }

    // ---- Derived values (never saved) ---------------------------------------------------------------------------------

    /// <summary>Part of <see cref="ChestsOpened"/> that belongs to teammates, on copies made by <see cref="WithExtras"/>.</summary>
    [JsonIgnore]
    public int TeammatesAdded { get; private set; }

    /// <summary>Chests of the player alone (the extra kinds chosen in Settings included, teammates excluded).</summary>
    [JsonIgnore]
    public int OwnChests => ChestsOpened - TeammatesAdded;

    /// <summary>Count of one non-chest kind. Matches saved by early versions only knew ammo boxes, in their own field.</summary>
    public int Interactions(string kind)
    {
        if (OtherInteractions.Count == 0)
        {
            return kind == ChestKinds.AmmoBox ? AmmoBoxesOpened : 0;
        }
        return OtherInteractions.TryGetValue(kind, out var count) ? count : 0;
    }

    /// <summary>What one teammate was seen opening: chests plus the extra kinds being counted.</summary>
    public static int Seen(TeammateRecord mate, IReadOnlyCollection<string> kinds)
    {
        var total = mate.ChestsSeen;
        foreach (var kind in kinds)
        {
            total += mate.Kinds.TryGetValue(kind, out var n) ? n : 0;
        }
        return total;
    }

    /// <summary>Sum of <see cref="Seen"/> over the squad. A lower bound: far-away teammates are not in the replay.</summary>
    public int TeammatesSeen(IReadOnlyCollection<string> kinds)
    {
        if (Teammates == null)
        {
            return 0;
        }
        var total = 0;
        foreach (var mate in Teammates)
        {
            total += Seen(mate, kinds);
        }
        return total;
    }

    /// <summary>
    /// The match as the player chose to count it: extra kinds (ammo boxes…) and, optionally, the teammates' chests
    /// added to <see cref="ChestsOpened"/>. Works on a shallow copy so the stored history stays untouched; when there
    /// is nothing to add, the same instance comes back.
    /// </summary>
    public MatchRecord WithExtras(IReadOnlyCollection<string> kinds, bool addTeammates = false)
    {
        var extraKinds = 0;
        foreach (var kind in kinds)
        {
            extraKinds += Interactions(kind);
        }
        var fromMates = addTeammates ? TeammatesSeen(kinds) : 0;
        if (extraKinds + fromMates == 0)
        {
            return this;
        }

        var counted = (MatchRecord)MemberwiseClone();
        counted.ChestsOpened += extraKinds + fromMates;
        counted.TeammatesAdded = fromMates;
        return counted;
    }
}
