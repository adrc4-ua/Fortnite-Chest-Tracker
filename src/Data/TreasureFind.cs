namespace FCT.Data;

/// <summary>
/// One treasure-hunt reward of a match (data.json format): what it was, where it came from, who got it and how sure
/// FCT is about each of those.
/// </summary>
public sealed class TreasureFind
{
    public const string FinderSelf = "self", FinderTeammate = "teammate", FinderUnknown = "unknown";

    // ---- What ------------------------------------------------------------------------------------------------------------

    public string Item { get; set; } = "";
    public string RawItem { get; set; } = "";

    /// <summary>Server prize id of a featured reward ("DelilahPrize:dpid_…"); gizmos get an internal "Unknown:…" key.</summary>
    public string? PrizeId { get; set; }

    /// <summary>Gizmo name read from the end-of-match screen (the replay never says which gizmo it was).</summary>
    public string? ScreenName { get; set; }

    /// <summary>Name to show, looked up every time so a rename in treasures.json reaches old matches too.</summary>
    public string DisplayName => Prizes.Describe(this).Name;

    /// <summary>A named reward from the server (cosmetic, vehicle, shop access…) rather than one of the gizmos.</summary>
    public bool Featured => FromPrizeChest || NewAccountItem || (PrizeId is { } id && !TreasureNames.IsGizmoKey(id));

    // ---- When and how it was detected ---------------------------------------------------------------------------------

    public float? ObtainedAtSeconds { get; set; }

    /// <summary>The time is the real one (prize chest or prize handler); otherwise it is estimated.</summary>
    public bool TimeExact { get; set; }

    /// <summary>Found through the treasure chest itself, which carries the prize id.</summary>
    public bool FromPrizeChest { get; set; }

    /// <summary>Found only through the prize handler: exact time, item known only if sent at that moment.</summary>
    public bool FromPrizeHandler { get; set; }

    /// <summary>Only on the end-of-match screen, not in the replay (won after being eliminated): no time, no finder.</summary>
    public bool FromScreen { get; set; }

    /// <summary>
    /// Named through the game's list of owned prizes (<see cref="TrackerData.PrizeLists"/>): the replay saw a treasure
    /// (or none) but not its golden chest. <see cref="GenericPrizeId"/> keeps what it was before, so it can be undone.
    /// </summary>
    public bool FromPrizeList { get; set; }

    /// <summary>Added by the prize list: the replay did not see this treasure at all.</summary>
    public bool AddedFromPrizeList { get; set; }

    /// <summary>The prize list only narrows the win down to a few matches: this is the most likely one.</summary>
    public bool PrizeMatchLikely { get; set; }

    /// <summary>
    /// The game's log shows the server adding something to the player's account in the same second as this treasure's
    /// grant (<see cref="TrackerData.AccountGrants"/>): a new featured treasure, even when its golden chest was opened
    /// out of the replay's range. Set again from the log every time (<see cref="FCT.PostMatch.AccountGrantMerge"/>).
    /// </summary>
    public bool NewAccountItem { get; set; }

    /// <summary>
    /// Named by elimination: of this week's featured prizes, only this one was not owned yet when it was won
    /// (<see cref="GenericPrizeId"/> keeps what it was before, so it can be undone).
    /// </summary>
    public bool PrizeDeduced { get; set; }

    /// <summary>
    /// Named by the "Treasure found!" screen the game showed back in the lobby after this match
    /// (<see cref="TrackerData.GiftScreens"/>); <see cref="GenericPrizeId"/> keeps what it was before.
    /// </summary>
    public bool FromGiftScreen { get; set; }

    /// <summary>Added by that screen: the replay did not see this treasure at all.</summary>
    public bool AddedFromGiftScreen { get; set; }

    /// <summary>The prize id and item the treasure had before the prize list (or the elimination) named it.</summary>
    public string? GenericPrizeId { get; set; }
    public string? GenericItem { get; set; }

    // ---- Where it came from ------------------------------------------------------------------------------------------------

    public string SourceKind { get; set; } = "";
    public string SourceBlueprint { get; set; } = "";
    public bool SourceIsChest { get; set; } = true;
    public bool SourceDestroyed { get; set; }

    /// <summary>The source container happened at the very moment of the reward (false: only the closest one in time).</summary>
    public bool SourceCertain { get; set; }

    /// <summary>For an estimated source: non-chest searches between that source and the first use of the reward.</summary>
    public List<string> AlsoPossible { get; set; } = new();

    public string SourceLabel
    {
        get
        {
            var kind = L.Name(ChestKinds.Shown(SourceKind));
            return SourceDestroyed ? $"{kind} ({L.T("destruido", "destroyed")})" : kind;
        }
    }

    // ---- Who got it -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// <see cref="FinderSelf"/> (also every treasure stored before analyzer 15, which only kept the player's own),
    /// <see cref="FinderTeammate"/> (named in <see cref="FinderName"/>) or <see cref="FinderUnknown"/> (the squad got
    /// it but its chest is not in the replay: a teammate out of range).
    /// </summary>
    public string Finder { get; set; } = FinderSelf;

    public string? FinderName { get; set; }

    /// <summary>Text of the "obtained by" column of the stats page.</summary>
    public string FinderLabel(MatchRecord match)
    {
        static string OrDefault(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name;
        return Finder switch
        {
            FinderTeammate => OrDefault(FinderName, L.T("Compañero", "Teammate")),
            FinderUnknown => L.T("Desconocido", "Unknown"),
            _ => OrDefault(match.PlayerName, L.T("Tú", "You")),
        };
    }
}
