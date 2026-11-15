using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FCT.ReplayModels;
using FortniteReplayReader.Models;
using FortniteReplayReader.Models.NetFieldExports;
using FortniteReplayReader.Models.NetFieldExports.Items;
using Unreal.Core;
using Unreal.Core.Contracts;
using Unreal.Core.Exceptions;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace FCT.Analysis;

/// <summary>
/// FCT's reading of a replay on top of the replay library: it follows containers, pickups, gizmos, featured treasures,
/// treasure grants and who is who, into <see cref="ReplaySignals"/>. It also lets the library read what FCT needs
/// beyond what it supports (unknown container blueprints, replays still being written, the raw ability payloads).
/// </summary>
internal sealed class SignalReader : FortniteReplayReader.ReplayReader
{
    private static readonly Regex TreasureActor = new(@"FlameCeleb|RelicExtractor|RelicFinder|LuckyLocator|SmugglerExtractionKey|ExtractionAccelerat", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrackedObject = new(@"TreasureHandler|WID_(FlameCeleb|RelicExtractor|RelicFinder|LuckyLocator|SmugglerExtractionKey)", RegexOptions.Compiled);
    /// <summary>"DelilahPrize:dpid_item17", or a bare GUID (the Pirate Shop Access, the only prize with one).</summary>
    private static readonly Regex PrizeId = new(@"[A-Za-z][A-Za-z0-9_]*:[A-Za-z0-9_.\-]+|[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", RegexOptions.Compiled);

    /// <summary>A golden chest whose prize id could not be read: still a featured treasure, just without a name.</summary>
    public const string UnreadPrize = "DelilahPrize:unread";
    private static readonly Regex InstanceTail = new(@"_UAID_.*$", RegexOptions.Compiled);

    static SignalReader()
    {
        // FCT's own models live in their own assembly: load it before the library's parser looks for models.
        _ = typeof(AnyContainer).Assembly;
        ReplayAliases.Group = GroupAlias;
        ReplayAliases.ClassNetCache = path =>
            path.EndsWith("_ClassNetCache", StringComparison.Ordinal) && path.Contains("LootCannon", StringComparison.OrdinalIgnoreCase) ? LootCannonCalls.CachePath : null;
    }

    /// <summary>
    /// Group paths the library has no model for, read as one FCT has: treasure-hunt golden chests (variants included),
    /// gizmos in hand, and any container or chest blueprint.
    /// </summary>
    private static string? GroupAlias(string path)
    {
        if (path.Contains(':') || path.EndsWith("_ClassNetCache", StringComparison.Ordinal))
        {
            return null;
        }
        var name = path[(path.LastIndexOf('/') + 1)..];
        if (name.StartsWith("BGA_DelilahTreasureHuntAnimatedChest", StringComparison.Ordinal))
        {
            return TreasurePayout.Path; // its variants ("…_Rare") carry the prize id too, despite "Chest" in the name
        }
        if (name.StartsWith("B_", StringComparison.Ordinal) && TreasureActor.IsMatch(name))
        {
            return AnyGizmo.Path;
        }
        var container = path.Contains("/Containers/", StringComparison.OrdinalIgnoreCase) || name.Contains("Chest", StringComparison.OrdinalIgnoreCase);
        return container ? AnyContainer.Path : null;
    }

    private readonly ReplaySignals _signals;
    private readonly ContainerLog _containers;
    private long _fileLength;
    private uint _bunchChannel;

    public SignalReader(ReplaySignals signals)
        : base(Environment.GetEnvironmentVariable("FCT_DEBUG") == "2" ? new StderrLogger() : null!, // no logger unless debugging
               Enum.TryParse<ParseMode>(Environment.GetEnvironmentVariable("FCT_MODE"), out var mode) ? mode : ParseMode.Normal)
    {
        _signals = signals;
        _containers = new ContainerLog(signals);
        StderrLogger.Clock = () => Now;
    }

    /// <summary>Replay time of the frame being read.</summary>
    public float Now { get; private set; }

    public FortniteReplay Read(Stream stream)
    {
        _fileLength = stream.Length;
        return ReadReplay(stream);
    }

    // ---- Reading the file ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Walks the file's chunks like the library, but tolerates a replay still being written: it stops at the last
    /// complete chunk, skips event chunks that cannot be decrypted yet and stops at a data chunk that cannot.
    /// </summary>
    public override void ReadReplayChunks(FArchive archive)
    {
        while (!archive.AtEnd())
        {
            if (_fileLength - archive.Position < 8)
            {
                return; // not even a chunk header left
            }
            var type = archive.ReadUInt32AsEnum<ReplayChunkType>();
            var size = archive.ReadInt32();
            var start = archive.Position;
            if (size > 0 && start + (long)size > _fileLength)
            {
                return; // last chunk still being written
            }
            if (size <= 0 || start + (long)size > int.MaxValue)
            {
                archive.SetError();
                return;
            }

            var live = Replay.Info?.IsLive == true;
            switch (type)
            {
                case ReplayChunkType.Header:
                    ReadReplayHeader(archive);
                    break;
                case ReplayChunkType.ReplayData when _parseMode > ParseMode.EventsOnly:
                    try
                    {
                        ReadReplayData(archive, size);
                    }
                    catch (CryptographicException) when (live)
                    {
                        return; // the rest of a live replay cannot be read yet
                    }
                    break;
                case ReplayChunkType.Event:
                    try
                    {
                        ReadEvent(archive);
                    }
                    catch (CryptographicException) when (live)
                    {
                        // an event of a live replay that is not final yet: the data chunks are what matters
                    }
                    break;
                // Checkpoints only serve to jump around a replay: not needed when reading it whole.
            }
            if (archive.Position != start + size)
            {
                archive.Seek(start + size, SeekOrigin.Begin);
            }
        }
    }

    /// <summary>Events the library does not know (new ones come with game updates) are skipped instead of failing.</summary>
    public override void ReadEvent(FArchive archive)
    {
        try
        {
            base.ReadEvent(archive);
        }
        catch (UnknownEventException)
        {
        }
    }

    /// <summary>Takes the frame's time before the library reads the frame.</summary>
    public override void ReadDemoFrameIntoPlaybackPackets(FArchive archive)
    {
        var start = archive.Position;
        if (archive.NetworkVersion >= NetworkVersionHistory.HISTORY_MULTIPLE_LEVELS)
        {
            archive.ReadInt32(); // level index
        }
        Now = archive.ReadSingle();
        archive.Seek(start, SeekOrigin.Begin);
        base.ReadDemoFrameIntoPlaybackPackets(archive);
    }

    /// <summary>
    /// Notes when the tracked objects are first sent: the treasure handler class arrives the first time the recording
    /// player's squad gets a treasure, and its id is what the ability grants refer to.
    /// </summary>
    public override NetworkGUID InternalLoadObject(FArchive archive, bool isExportingNetGUIDBunch, int internalLoadObjectRecursionCount = 0)
    {
        var id = base.InternalLoadObject(archive, isExportingNetGUIDBunch, internalLoadObjectRecursionCount);
        if (isExportingNetGUIDBunch && id.IsValid() && _netGuidCache.TryGetPathName(id.Value, out var path) && TrackedObject.IsMatch(path))
        {
            var name = ShortName(path);
            _signals.FirstSent.TryAdd(name, Now);
            if (name.Contains("TreasureHandler", StringComparison.Ordinal))
            {
                _signals.HandlerIds[id.Value] = name.Replace("Default__", "");
            }
        }
        return id;
    }

    // ---- The ability system's raw payload -------------------------------------------------------------------------------

    public override bool ReceivedReplicatorBunch(DataBunch bunch, FBitArchive archive, uint? repObject, bool bHasRepLayout)
    {
        _bunchChannel = bunch.ChIndex;
        var group = _netGuidCache.GetNetFieldExportGroup(repObject);
        if (group != null && IsAbilitySystem(group.PathName) && !_netFieldParser.WillReadType(group.PathName))
        {
            return SkimUnknownAbilitySystem(bunch, archive, group, bHasRepLayout);
        }
        return base.ReceivedReplicatorBunch(bunch, archive, repObject, bHasRepLayout);
    }

    /// <summary>
    /// The abilities field of an ability system ClassNetCache (named "ActivatableAbilities", or unnamed in the variants
    /// without exported names) is taken raw here and searched for treasure grants; the library never sees it.
    /// </summary>
#pragma warning disable CS8765 // unlike the base, a consumed field comes back as true with no payload (handled by the library patch)
    public override bool ReadFieldHeaderAndPayload(FBitArchive archive, NetFieldExportGroup group, out NetFieldExport? outField, out uint? payload)
#pragma warning restore CS8765
    {
        var more = base.ReadFieldHeaderAndPayload(archive, group, out outField, out payload);
        if (!more || payload is not { } size || !IsAbilitySystem(group.PathName) || !group.PathName.EndsWith("_ClassNetCache", StringComparison.Ordinal)
            || !(outField == null || outField.Name == "ActivatableAbilities"))
        {
            return more;
        }
        archive.SetTempEnd((int)size, FBitArchiveEndIndex.FIELD_HEADER_PAYLOAD);
        try
        {
            var bits = archive.GetBitsLeft();
            if (bits > 0 && !archive.IsError && _signals.HandlerIds.Count > 0)
            {
                CollectGrants(archive.ReadBits(bits).ToArray(), bits);
            }
        }
        finally
        {
            archive.RestoreTempEnd(FBitArchiveEndIndex.FIELD_HEADER_PAYLOAD); // the position ends after the payload
        }
        payload = null; // already consumed: the caller moves on to the next field
        return true;
    }

    private static bool IsAbilitySystem(string path) => path.Contains("FortAbilitySystemComponent", StringComparison.Ordinal);

    /// <summary>
    /// An ability system group the library has no model for: its properties are skipped, and its ClassNetCache fields
    /// go through <see cref="ReadFieldHeaderAndPayload"/> (where the abilities field is read).
    /// </summary>
    private bool SkimUnknownAbilitySystem(DataBunch bunch, FBitArchive archive, NetFieldExportGroup group, bool hasRepLayout)
    {
        if (hasRepLayout)
        {
            archive.ReadBit(); // checksum flag
            for (var handle = archive.ReadIntPacked(); handle != 0 && !archive.IsError; handle = archive.ReadIntPacked())
            {
                var bits = archive.ReadIntPacked();
                if (archive.IsError || bits > archive.GetBitsLeft())
                {
                    return false;
                }
                archive.SkipBits((int)bits);
            }
        }
        if (archive.AtEnd())
        {
            return true;
        }
        var fullName = bunch.Archive.EngineNetworkVersion >= EngineNetworkVersionHistory.HISTORY_CLASSNETCACHE_FULLNAME;
        if (!_netGuidCache.TryGetClassNetCache(group.PathName, out var cache, fullName))
        {
            return false;
        }
        while (ReadFieldHeaderAndPayload(archive, cache, out _, out var payload))
        {
            if (payload is { } size)
            {
                archive.SetTempEnd((int)size, FBitArchiveEndIndex.FIELD_HEADER_PAYLOAD);
                archive.RestoreTempEnd(FBitArchiveEndIndex.FIELD_HEADER_PAYLOAD); // skip it
            }
        }
        return true;
    }

    private void CollectGrants(byte[] data, int bits)
    {
        var playerState = Channels[_bunchChannel]?.ActorId ?? 0;
        foreach (var (element, handler) in AbilityGrants.Find(data, bits, _signals.HandlerIds))
        {
            if (!_signals.Grants.Exists(g => g.PlayerState == playerState && g.Element == element))
            {
                _signals.Grants.Add(new ReplaySignals.Grant(Now, playerState, element, handler));
            }
        }
    }

    // ---- Actors and their state ---------------------------------------------------------------------------------------------

    protected override void OnChannelOpened(uint channelIndex, NetworkGUID? actor)
    {
        base.OnChannelOpened(channelIndex, actor);
        var channel = Channels[channelIndex];
        var archetype = PathOf(channel?.ArchetypeId);
        var actorPath = PathOf(channel?.ActorId);
        var label = ShortName(archetype.Length > 0 ? archetype : actorPath);

        string key;
        if (actorPath.Length > 0)
        {
            key = actorPath; // placed in the level: its path is unique and stable
        }
        else if (channel?.Actor?.Location is { } at && IsSpawnedChest(label))
        {
            key = $"{label}@{at.X:F0},{at.Y:F0}"; // spawned (buried, relic…): new id each time, same spot all match
        }
        else
        {
            key = $"guid:{channel?.ActorId}";
        }
        _containers.Opened(channelIndex, key, label, Now);
    }

    /// <summary>A chest that appears during the match (the sand pile of a buried chest and other props are not).</summary>
    private static bool IsSpawnedChest(string label) =>
        !label.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase) && ChestKinds.IsChest(ChestKinds.Classify(label));

    protected override void OnExportRead(uint channelIndex, INetFieldExportGroup? exportGroup)
    {
        base.OnExportRead(channelIndex, exportGroup);
        switch (exportGroup)
        {
            case BaseContainer container:
                _containers.State(channelIndex, container, Now);
                break;
            case LootCannonShot shot when shot.InteractingPawn is { } gunner && gunner != 0:
                _containers.CannonFired(channelIndex, gunner, Now);
                break;
            case TreasurePayout payout:
                if (ReadPrizeId(payout.PrizeTemplateId) is { } prize && payout.bSuccess != false && _containers.FirstPayout(channelIndex))
                {
                    _signals.Payouts.Add(new ReplaySignals.Payout(Now, prize, payout.OpeningPawn ?? payout.Instigator, PathOf(payout.SourceContainer)));
                }
                break;
            case AnyGizmo gizmo:
                var holder = gizmo.Instigator ?? gizmo.Owner?.Value;
                if (holder is { } pawn && pawn != 0 && _containers.FirstEquip(channelIndex))
                {
                    var item = gizmo.WeaponData?.Name;
                    _signals.Equips.Add(new ReplaySignals.Equip(Now, string.IsNullOrEmpty(item) ? _containers.Label(channelIndex, "?") : item, pawn));
                }
                break;
            case FortPickup pickup when pickup.bTossedFromContainer == true && pickup.ItemDefinition != null:
                var tossed = pickup.ItemDefinition.Name;
                if (string.IsNullOrEmpty(tossed))
                {
                    tossed = ShortName(PathOf(pickup.ItemDefinition.Value));
                }
                if (!string.IsNullOrEmpty(tossed))
                {
                    _signals.Tosses.Add(new ReplaySignals.Toss(Now, tossed));
                }
                break;
            case PlayerPawn playerPawn:
                if (Channels[channelIndex]?.ActorId is { } pawnId)
                {
                    if (playerPawn.PlayerState is { } state && state != 0)
                    {
                        _signals.PawnState[pawnId] = state;
                    }
                    if (playerPawn.bIsSkydivingFromBus == true)
                    {
                        _signals.BusJumps.TryAdd(pawnId, Now); // left the bus
                    }
                }
                break;
            case FortPlayerState playerState:
                var playerId = playerState.PlayerId is { } id ? (int?)id : playerState.PlayerID;
                if (Channels[channelIndex]?.ActorId is { } stateId && playerId != null)
                {
                    _signals.StatePlayer[stateId] = playerId.Value;
                }
                break;
        }
    }

    // ---- Names ---------------------------------------------------------------------------------------------------------------

    public string PathOf(uint? id) => id is { } value && value != 0 && _netGuidCache.TryGetPathName(value, out var path) ? path : "";

    /// <summary>The object's own name: last path segment, after the last dot, without an instance tail.</summary>
    public static string ShortName(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return InstanceTail.Replace(name[(name.LastIndexOf('.') + 1)..], "");
    }

    /// <summary>"DelilahPrize:dpid_item4" out of the raw bits of the prize property (a string inside a struct).</summary>
    private static string? ReadPrizeId(DebuggingObject? raw)
    {
        if (raw == null || raw.TotalBits == 0)
        {
            return null;
        }
        var text = string.Create(raw.Bytes.Length, raw.Bytes, (chars, bytes) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = bytes[i] is >= 32 and < 127 ? (char)bytes[i] : ' ';
            }
        });
        var found = PrizeId.Match(text);
        return found.Success ? found.Value : UnreadPrize;
    }

    /// <summary>FCT_DEBUG=2: the library's own log, on the error output, with the replay time.</summary>
    private sealed class StderrLogger : Microsoft.Extensions.Logging.ILogger
    {
        public static Func<float> Clock { get; set; } = () => 0;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => level >= Microsoft.Extensions.Logging.LogLevel.Information;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) =>
            Console.Error.WriteLine($"LOG {level} {Clock(),7:F1}s {format(state, error)}");
    }
}
