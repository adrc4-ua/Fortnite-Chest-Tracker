namespace FCT.Data;

/// <summary>
/// A backup is the exact state of the player's data when it was made: the history (data.json: matches with their chests,
/// treasures and teammates, which replays were read, treasure screens, the game's prize lists) beside
/// "…state.json" with what config.json and treasures.json hold of the player's own (<see cref="BackupState"/>).
/// Restoring brings all of it back as it was; only the settings without data (language, overlay, updates…) stay as
/// they are now. Backups of earlier versions only kept the chests from before FCT beside the history ("…earlier.json").
/// </summary>
public static class Backup
{
    /// <summary>The player's own data beside a backup's history.</summary>
    public static string StateOf(string backup) => Path.ChangeExtension(backup, null) + ".state.json";

    /// <summary>Writes a backup of this history and this data to <paramref name="path"/>.</summary>
    public static void Write(string path, TrackerData data, BackupState state)
    {
        JsonFile.Write(StateOf(path), state); // first: a backup is only listed once its history is there
        JsonFile.Write(path, data);
    }

    /// <summary>The backup's history and data, or null when its history cannot be read.</summary>
    public static (TrackerData Data, BackupState State)? Read(string path)
    {
        if (JsonFile.Read<TrackerData>(path) is not { } data)
        {
            return null;
        }
        var state = JsonFile.Read<BackupState>(StateOf(path));
        if (state == null)
        {
            // a backup of an earlier version: its chests from before FCT, if it kept them (nothing else was kept)
            var earlier = JsonFile.Read<Dictionary<string, int>>(Store.EarlierChestsOf(path)) ?? new Dictionary<string, int>();
            state = new BackupState { InitialChests = earlier, Complete = false };
        }
        return (data, state);
    }

    /// <summary>
    /// Makes <paramref name="current"/> exactly the backup's history. Changed in place: the tracker and its replays
    /// folder keep these same lists.
    /// </summary>
    public static void Replace(TrackerData current, TrackerData backup)
    {
        current.Version = backup.Version;
        current.AnalyzerVersion = backup.AnalyzerVersion;
        current.CreatedUtc = backup.CreatedUtc;
        current.Matches.Clear();
        current.Matches.AddRange(backup.Matches);
        current.ProcessedFiles.Clear();
        foreach (var (file, verdict) in backup.ProcessedFiles)
        {
            current.ProcessedFiles[file] = verdict;
        }
        current.TreasureScreens.Clear();
        current.TreasureScreens.AddRange(backup.TreasureScreens);
        current.PrizeLists.Clear();
        current.PrizeLists.AddRange(backup.PrizeLists);
    }
}

/// <summary>What config.json and treasures.json hold of the player's own data, as a backup keeps it.</summary>
public sealed class BackupState
{
    /// <summary>Chests from before FCT, per profile.</summary>
    public Dictionary<string, int> InitialChests { get; set; } = new();

    /// <summary>The estimates behind them, typed averages included, per profile.</summary>
    public Dictionary<string, HistoryEstimate> Estimates { get; set; } = new();

    /// <summary>Prizes of the weekly lists struck off or brought back by hand.</summary>
    public Dictionary<string, bool> PrizeMarks { get; set; } = new();

    /// <summary>The names the player gave to prizes (treasures.json).</summary>
    public Dictionary<string, string> PrizeNames { get; set; } = new();

    /// <summary>False for a backup of an earlier version, which only kept <see cref="InitialChests"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Complete { get; init; } = true;

    /// <summary>This config's data, with the prize names saved in <paramref name="store"/>.</summary>
    public static BackupState Of(Config config, Store store) => Of(config, JsonFile.Read<Dictionary<string, string>>(store.PrizeNamesPath));

    public static BackupState Of(Config config, Dictionary<string, string>? prizeNames) => new()
    {
        InitialChests = new Dictionary<string, int>(config.InitialChests),
        Estimates = Copy(config.Estimates),
        PrizeMarks = new Dictionary<string, bool>(config.PrizeMarks),
        PrizeNames = prizeNames == null ? new Dictionary<string, string>() : new Dictionary<string, string>(prizeNames),
    };

    /// <summary>Puts this data into <paramref name="config"/>, replacing what it has (an earlier version's backup: only its chests from before FCT).</summary>
    public void ApplyTo(Config config)
    {
        config.InitialChests = new Dictionary<string, int>(InitialChests);
        if (!Complete)
        {
            return;
        }
        config.Estimates = Copy(Estimates);
        config.PrizeMarks = new Dictionary<string, bool>(PrizeMarks);
    }

    /// <summary>Is anything here: a backup with no matches is still worth making if it holds this.</summary>
    public bool HasAny => InitialChests.Count > 0 || Estimates.Count > 0 || PrizeMarks.Count > 0 || PrizeNames.Values.Any(name => name.Length > 0);

    /// <summary>A deep copy (estimates are objects the settings change in place).</summary>
    private static Dictionary<string, HistoryEstimate> Copy(Dictionary<string, HistoryEstimate> estimates) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, HistoryEstimate>>(
            System.Text.Json.JsonSerializer.Serialize(estimates, JsonFile.Options), JsonFile.Options) ?? new();
}
