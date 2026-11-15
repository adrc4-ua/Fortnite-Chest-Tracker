namespace FCT.Data;

/// <summary>Root of data.json: the history and the bookkeeping needed to read each replay only once.</summary>
public sealed class TrackerData
{
    public int Version { get; set; } = 1;
    public int AnalyzerVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<MatchRecord> Matches { get; set; } = new();

    /// <summary>
    /// Replays already handled. Key: "name|size|last-write ticks"; value: the session id it produced, "skipped", or
    /// "error: …" with the reason.
    /// </summary>
    public Dictionary<string, string> ProcessedFiles { get; set; } = new();

    /// <summary>
    /// In a view (<see cref="FCT.Analysis.Profiles.View"/>): matches of the profile left without jumping from the bus.
    /// They are out of <see cref="Matches"/> and only add to the matches played: here, when each one started (UTC).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<DateTime> UnjumpedStarts { get; set; } = new();

    /// <summary>How many matches of the view were left without jumping from the bus.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int UnjumpedMatches => UnjumpedStarts.Count;

    /// <summary>End-of-match treasure screens read so far; kept so they can be matched again after a re-analysis.</summary>
    public List<TreasureScreen> TreasureScreens { get; set; } = new();

    /// <summary>
    /// The game's own list of the featured prizes the player owns, each time it changed (from the game's log): the
    /// exact source of which featured treasure was won, even when the replay did not see its golden chest.
    /// </summary>
    public List<PrizeList> PrizeLists { get; set; } = new();
}

/// <summary>The featured prizes the player owned, as the game listed them over a stretch of time.</summary>
public sealed class PrizeList
{
    /// <summary>When the game first listed exactly these prizes (UTC).</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>The last time it listed them unchanged (UTC).</summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>Prize ids ("DelilahPrize:dpid_item21", the Pirate Shop Access's GUID).</summary>
    public List<string> Ids { get; set; } = new();
}
