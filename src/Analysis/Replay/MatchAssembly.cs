using System.Text.RegularExpressions;
using FortniteReplayReader.Models;

namespace FCT.Analysis;

/// <summary>
/// From what the replay showed (<see cref="ReplaySignals"/>) to the match of one player: who is tracked, their chests,
/// their squad and the treasures the squad received.
/// </summary>
internal sealed class MatchAssembly
{
    // Treasure-hunt items, or anything named like a treasure, among the loot of a chest.
    private static readonly Regex TreasureLoot = new(@"RelicExtractor|RelicFinder|LuckyLocator|FlameCeleb|SmugglerExtractionKey|ExtractionAccelerat|Treasure|PirateShop|DelilahShop", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Limited-time loot that looks like the treasure extractor but is ordinary loot (no treasure is granted for it).
    public static readonly Regex EventLoot = new(@"RelicExtractor_Consumable", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public FortniteReplay Replay { get; }
    public ReplaySignals Signals { get; }

    public PlayerData? Target { get; private set; }
    public PlayerData? Recorder { get; private set; }
    public bool MatchedByName { get; private set; }
    public bool NotTracked { get; private set; }
    public HashSet<uint> TargetStates { get; private set; } = new();

    /// <summary>The tracked player left the battle bus (one of their pawns skydived from it).</summary>
    public bool JumpedFromBus => Signals.PawnsOf(TargetStates).Any(Signals.BusJumps.ContainsKey);
    public HashSet<uint> TargetPawns { get; private set; } = new();
    public bool RecorderIsTarget { get; private set; }

    public List<(float Time, string Kind, string Label, string Key, bool Destroyed)> Interactions { get; private set; } = new();
    public List<ChestRecord> Chests { get; } = new();
    public Dictionary<string, int> Others { get; } = new();
    public List<TeammateRecord> Teammates { get; } = new();

    /// <summary>Pawn of a teammate → their name.</summary>
    public Dictionary<uint, string> MatePawns { get; } = new();

    public List<TreasureFind> Treasures { get; } = new();
    public List<(float Time, string Handler)> HandlerGrants { get; private set; } = new();

    /// <summary>Featured treasures confirmed by a grant.</summary>
    public HashSet<TreasureFind> ConfirmedPayouts { get; } = new();

    public MatchAssembly(FortniteReplay replay, ReplaySignals signals, string? username)
    {
        Replay = replay;
        Signals = signals;
        FindTarget(username);
        ReadChests();
        ReadSquad();
        ReadPayouts();
        ReadGrants();
        Treasures.Sort((a, b) => (a.ObtainedAtSeconds ?? float.MaxValue).CompareTo(b.ObtainedAtSeconds ?? float.MaxValue));
    }

    private List<PlayerData> Players => Replay.PlayerData?.ToList() ?? new List<PlayerData>();

    /// <summary>
    /// Configured names (comma-separated) win: if none of them played, the match belongs to nobody tracked. Without
    /// names, the player who recorded the replay is tracked (the replays on a PC are its player's).
    /// </summary>
    private void FindTarget(string? username)
    {
        var players = Players;
        var names = (username ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length > 0)
        {
            Target = players.FirstOrDefault(p => Array.Exists(names, n => string.Equals(p.PlayerName?.Trim(), n, StringComparison.OrdinalIgnoreCase)));
            MatchedByName = Target != null;
        }

        var recorderState = Replay.GameData?.RecorderId;
        if (recorderState is { } rs && Signals.StatePlayer.TryGetValue(rs, out var recorderId))
        {
            Recorder = players.FirstOrDefault(p => p.Id == recorderId);
        }
        NotTracked = names.Length > 0 && Target == null;
        if (Target == null && !NotTracked)
        {
            Target = Recorder;
        }

        TargetStates = Target?.Id != null ? Signals.StatesOf(Target.Id) : new HashSet<uint>();
        if (TargetStates.Count == 0 && recorderState != null && !MatchedByName && !NotTracked)
        {
            TargetStates.Add(recorderState.Value);
        }
        RecorderIsTarget = recorderState != null && TargetStates.Contains(recorderState.Value);
        TargetPawns = Signals.PawnsOf(TargetStates);
    }

    /// <summary>The target's searches: chests (with what fell out of them) and every other kind, counted apart.</summary>
    private void ReadChests()
    {
        Interactions = Signals.SearchesBy(TargetPawns)
            .OrderBy(s => s.Time)
            .Select(s => (s.Time, ChestKinds.Classify(s.Label), s.Label, s.Key, s.Destroyed))
            .ToList();
        foreach (var (time, kind, label, _, destroyed) in Interactions)
        {
            if (!ChestKinds.IsChest(kind))
            {
                Others[kind] = Others.GetValueOrDefault(kind) + 1;
                continue;
            }
            // What was thrown out of a chest lands from a quarter of a second before to just over half a second after.
            var loot = Signals.Tosses.Where(t => t.Time >= time - 0.25f && t.Time <= time + 0.6f).Select(t => t.Item).ToList();
            Chests.Add(new ChestRecord
            {
                TimeSeconds = MathF.Round(time, 1),
                Kind = kind,
                Blueprint = label,
                Destroyed = destroyed,
                Items = loot,
                TreasureItems = loot.Where(item => TreasureLoot.IsMatch(item) && !EventLoot.IsMatch(item)).ToList(),
            });
        }
    }

    /// <summary>The target's team, bots aside, with what each one was seen searching (only near the recording player).</summary>
    private void ReadSquad()
    {
        if (Target?.TeamIndex is not int team)
        {
            return;
        }
        var mates = Players
            .Where(p => p.TeamIndex == team && p.Id != Target.Id && p.IsBot != true)
            .GroupBy(p => p.EpicId ?? p.PlayerName ?? $"pid{p.Id}")
            .Select(sameAccount => sameAccount.First());
        foreach (var mate in mates)
        {
            var pawns = Signals.PawnsOf(Signals.StatesOf(mate.Id));
            var kinds = Signals.SearchesBy(pawns).GroupBy(s => ChestKinds.Classify(s.Label)).ToDictionary(k => k.Key, k => k.Count());
            var name = string.IsNullOrWhiteSpace(mate.PlayerName) ? L.T("Compañero", "Teammate") : mate.PlayerName.Trim();
            foreach (var pawn in pawns)
            {
                MatePawns[pawn] = name;
            }
            Teammates.Add(new TeammateRecord
            {
                Name = name,
                EpicId = mate.EpicId,
                ChestsSeen = kinds.Where(k => ChestKinds.IsChest(k.Key)).Sum(k => k.Value),
                Kinds = kinds,
            });
        }
        Teammates.Sort((a, b) => b.ChestsSeen.CompareTo(a.ChestsSeen));
    }

    private bool IsSquadPawn(uint pawn) => TargetPawns.Contains(pawn) || MatePawns.ContainsKey(pawn);

    /// <summary>
    /// Featured treasures (cosmetics, shop access): the golden chest names the prize, who opened the chest and which
    /// container it was. Only the target's and the teammates' count; an opponent's prize is not the squad's.
    /// </summary>
    private void ReadPayouts()
    {
        foreach (var payout in Signals.Payouts.OrderBy(p => p.Time))
        {
            if (payout.Pawn is not { } opener || !IsSquadPawn(opener))
            {
                continue;
            }
            var own = TargetPawns.Contains(opener);
            var label = SignalReader.ShortName(payout.Container);
            var kind = label.Length == 0 ? "unknown" : ChestKinds.Classify(label);
            Treasures.Add(new TreasureFind
            {
                Item = payout.PrizeId,
                RawItem = payout.PrizeId,
                PrizeId = payout.PrizeId,
                FromPrizeChest = true,
                TimeExact = true,
                ObtainedAtSeconds = MathF.Round(payout.Time, 1),
                SourceKind = kind,
                SourceBlueprint = label,
                SourceIsChest = ChestKinds.IsChest(kind),
                SourceCertain = true,
                SourceDestroyed = Signals.Searches.Exists(s => s.Destroyed && s.Key == payout.Container),
                Finder = own ? TreasureFind.FinderSelf : TreasureFind.FinderTeammate,
                FinderName = own ? null : MatePawns[opener],
            });
        }
    }

    /// <summary>
    /// Every treasure the squad receives grants a treasure handler to the recording player (exact time), whoever opened
    /// the chest: the only trace of a gizmo. A grant already explained by a featured treasure (within 5 s) is that one.
    /// Otherwise the source is the squad's search closest in time; a gizmo seen in someone's hands proves nothing
    /// (gizmos are kept between matches and also come from quests).
    /// </summary>
    private void ReadGrants()
    {
        HandlerGrants = Signals.Grants.Where(g => TargetStates.Contains(g.PlayerState)).OrderBy(g => g.Time).Select(g => (g.Time, g.Handler)).ToList();
        if (HandlerGrants.Count == 0 && RecorderIsTarget
            && Signals.FirstSent.Where(e => e.Key.Contains("TreasureHandler")).Select(e => (float?)e.Value).FirstOrDefault() is { } firstHandler)
        {
            HandlerGrants.Add((firstHandler, "GA_TreasureHandler")); // no grant decoded: the handler's first arrival marks the first treasure
        }

        foreach (var (time, handler) in HandlerGrants)
        {
            var payout = Treasures
                .Where(f => f.FromPrizeChest && !ConfirmedPayouts.Contains(f) && f.ObtainedAtSeconds is { } at && Math.Abs(at - time) <= 5)
                .OrderBy(f => Math.Abs(f.ObtainedAtSeconds!.Value - time))
                .FirstOrDefault();
            if (payout != null)
            {
                ConfirmedPayouts.Add(payout);
                continue;
            }
            Treasures.Add(Gizmo(time, handler));
        }
    }

    private TreasureFind Gizmo(float time, string handler)
    {
        // Buried chests grant the prize when the dig ends, ~2.5 s before they report "searched": up to +4 s for them only.
        var source = Signals.Searches
            .Where(s => s.Pawn is { } pawn && IsSquadPawn(pawn))
            .Where(s => s.Time >= time - 3 && (s.Time <= time + 2 || (s.Time <= time + 4 && ChestKinds.Classify(s.Label) == ChestKinds.Buried)))
            .OrderBy(s => Math.Abs(s.Time - time))
            .FirstOrDefault();
        var mine = source != null && TargetPawns.Contains(source.Pawn!.Value);
        var kind = source == null ? "unknown" : ChestKinds.Classify(source.Label);
        // Same instant (a broken chest reports ~0.6 s early, hence 1 s) or a buried chest's dig: certain; else likely.
        var certain = source != null && (Math.Abs(source.Time - time) <= 1f || (kind == ChestKinds.Buried && source.Time >= time));

        // The replay has no prize id for a gizmo: a key unique to this match and moment lets the player name it.
        var started = Replay.GameData?.UtcTimeStartedMatch ?? Replay.Info?.Timestamp ?? DateTime.UtcNow;
        var local = DateTime.SpecifyKind(started, DateTimeKind.Utc).ToLocalTime();
        var at = MathF.Round(time, 1);
        return new TreasureFind
        {
            Item = TreasureNames.UnknownGizmo,
            RawItem = handler,
            PrizeId = $"{TreasureNames.UnknownPrefix}{local:yyyy-MM-dd_HH.mm}@{(int)at / 60}m{(int)at % 60:00}s",
            ObtainedAtSeconds = at,
            TimeExact = true,
            FromPrizeHandler = true,
            SourceKind = kind,
            SourceBlueprint = source?.Label ?? "",
            SourceIsChest = source == null || ChestKinds.IsChest(kind), // unseen source (teammate far away): a chest, as every treasure
            SourceCertain = certain,
            SourceDestroyed = source?.Destroyed ?? false,
            Finder = source == null ? TreasureFind.FinderUnknown : mine ? TreasureFind.FinderSelf : TreasureFind.FinderTeammate,
            FinderName = source != null && !mine ? MatePawns[source.Pawn!.Value] : null,
        };
    }
}
