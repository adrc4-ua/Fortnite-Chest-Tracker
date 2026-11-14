namespace FCT.Analysis;

/// <summary>
/// The raw facts gathered while reading one replay, before deciding whose match it is. Times are replay seconds.
/// </summary>
internal sealed class ReplaySignals
{
    /// <summary>A container searched (or broken) by a pawn. <paramref name="Stale"/>: its state arrived together with
    /// the channel, i.e. it had been searched before it came into range.</summary>
    public sealed record Search(float Time, string Key, string Label, uint? Pawn, bool Destroyed = false, bool Stale = false);

    /// <summary>An item thrown out of a container.</summary>
    public sealed record Toss(float Time, string Item);

    /// <summary>A gizmo taken in hand.</summary>
    public sealed record Equip(float Time, string Item, uint Pawn);

    /// <summary>A featured treasure: the golden chest of the treasure hunt, with its prize id and source container.</summary>
    public sealed record Payout(float Time, string PrizeId, uint? Pawn, string Container);

    /// <summary>A treasure handler ability given to a player state (one per treasure the squad receives).</summary>
    public sealed record Grant(float Time, uint PlayerState, int Element, string Handler);

    public List<Search> Searches { get; } = new();
    public List<Toss> Tosses { get; } = new();
    public List<Equip> Equips { get; } = new();
    public List<Payout> Payouts { get; } = new();
    public List<Grant> Grants { get; } = new();

    /// <summary>Pawn actor → its player state actor.</summary>
    public Dictionary<uint, uint> PawnState { get; } = new();

    /// <summary>Pawns seen skydiving from the battle bus: their players left the bus (first time, replay seconds).</summary>
    public Dictionary<uint, float> BusJumps { get; } = new();

    /// <summary>Player state actor → player id (the one in the replay's player list).</summary>
    public Dictionary<uint, int> StatePlayer { get; } = new();

    /// <summary>First time each tracked object (treasure handlers, gizmo items) was sent, by short name.</summary>
    public Dictionary<string, float> FirstSent { get; } = new();

    /// <summary>Object ids of the treasure handler abilities → their name.</summary>
    public Dictionary<uint, string> HandlerIds { get; } = new();

    /// <summary>The player states of a player id.</summary>
    public HashSet<uint> StatesOf(int? playerId) => StatePlayer.Where(e => e.Value == playerId).Select(e => e.Key).ToHashSet();

    /// <summary>The pawns of some player states.</summary>
    public HashSet<uint> PawnsOf(IReadOnlySet<uint> states) => PawnState.Where(e => states.Contains(e.Value)).Select(e => e.Key).ToHashSet();

    /// <summary>Level path or spawn location: such a container keeps its key all match long (a guid or channel does not).</summary>
    public static bool StableKey(string key) => !key.StartsWith("guid:", StringComparison.Ordinal) && !key.StartsWith("ch:", StringComparison.Ordinal);

    /// <summary>Searches by these pawns that count (stale ones only for containers with a stable key).</summary>
    public IEnumerable<Search> SearchesBy(IReadOnlySet<uint> pawns) =>
        Searches.Where(s => s.Pawn is { } pawn && pawns.Contains(pawn) && (!s.Stale || StableKey(s.Key)));
}
