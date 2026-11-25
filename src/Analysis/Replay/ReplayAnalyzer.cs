namespace FCT.Analysis;

/// <summary>
/// Reads one .replay and returns its <see cref="MatchRecord"/>: <see cref="SignalReader"/> gathers what happened,
/// <see cref="MatchAssembly"/> decides whose match it is and what they got, and <see cref="MatchDiagnosis"/> explains it
/// on request (--diagnose).
/// </summary>
public sealed class ReplayAnalyzer
{
    /// <summary>
    /// Raise it whenever the analysis changes, so saved matches whose replay still exists are read again.
    /// 22: a golden chest seen twice (out of range and back) counts once, one already open when seen has no exact time, and a teammate outside the squad (bot) is no opponent.
    /// 21: a treasure's source is a chest, never an ammo box or another container. 20: the container of opponents' featured treasures. 19: featured treasures seen won by opponents. 18: the Pirate Shop Access's prize id (a GUID) and golden chests with an unreadable id. 17: whether the player left the battle bus. 16: final elimination recorded. 15: every treasure grant read, squad treasures with their finder.
    /// 14: chests behind instance names, refilled chests.
    /// </summary>
    public const int CurrentVersion = 22;

    /// <summary>Set to an empty list before <see cref="Analyze"/> to receive the cross-check of the match (--diagnose).</summary>
    public List<string>? Diagnosis { get; set; }

    /// <param name="username">Epic name(s) to track, comma-separated; empty tracks the player who recorded the replay.</param>
    public MatchRecord Analyze(Stream stream, string file, string? username)
    {
        var signals = new ReplaySignals();
        var replay = new SignalReader(signals).Read(stream);
        var match = new MatchAssembly(replay, signals, username);
        if (Diagnosis != null)
        {
            MatchDiagnosis.Write(match, Diagnosis);
        }
        return Record(match, file);
    }

    private static MatchRecord Record(MatchAssembly match, string file)
    {
        var replay = match.Replay;
        var info = replay.Info;
        var recorderState = replay.GameData?.RecorderId;
        var recorded = recorderState != null && match.TargetStates.Contains(recorderState.Value);
        var playlist = replay.GameData?.CurrentPlaylist;
        return new MatchRecord
        {
            SessionId = replay.GameData?.GameSessionId ?? $"{info?.Timestamp:O}-{Path.GetFileName(file)}",
            ReplayFile = Path.GetFileName(file),
            AnalyzedAtUtc = DateTime.UtcNow,
            AnalyzerVersion = CurrentVersion,
            MatchStartUtc = replay.GameData?.UtcTimeStartedMatch ?? info?.Timestamp,
            // the header keeps the local time the recording began (the one in the file name), to the millisecond
            RecordingStartUtc = info == null || info.Timestamp == default ? null : DateTime.SpecifyKind(info.Timestamp, DateTimeKind.Local).ToUniversalTime(),
            DurationSeconds = info == null ? null : (int)(info.LengthInMs / 1000),
            Playlist = playlist,
            GameVersion = replay.Header?.Branch,
            IsBattleRoyale = !string.IsNullOrEmpty(playlist) || match.Chests.Count > 0,
            JumpedFromBus = match.JumpedFromBus || match.Chests.Count > 0, // a chest opened: off the bus, whatever was seen

            PlayerName = match.Target?.PlayerName,
            EpicId = match.Target?.EpicId,
            TrackedByUsername = match.MatchedByName,
            NotTrackedOwner = match.NotTracked ? match.Recorder?.PlayerName ?? "unknown" : null,
            IsReplayOwner = recorded,
            Placement = match.Target?.Placement ?? (recorded ? (int?)replay.TeamStats?.Position : null),
            TotalPlayers = (int?)replay.TeamStats?.TotalPlayers,
            Eliminations = recorded ? (int?)replay.Stats?.Eliminations : null,

            ChestsOpened = match.Chests.Count,
            AmmoBoxesOpened = match.Others.GetValueOrDefault(ChestKinds.AmmoBox),
            OtherContainersOpened = match.Others.Where(o => o.Key != ChestKinds.AmmoBox).Sum(o => o.Value),
            OtherInteractions = match.Others,
            Chests = match.Chests,
            TreasureFinds = match.Treasures,
            RivalTreasures = match.RivalTreasures(),
            Teammates = match.Teammates,
        };
    }
}
