using System.Text.Json.Serialization;

namespace FCT.Data;

/// <summary>
/// Settings saved in config.json. The JSON names are the file format (some differ from the property name on purpose,
/// see <see cref="JsonPropertyNameAttribute"/>); defaults are what a new installation gets.
/// </summary>
public sealed class Config
{
    // ---- General ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// App language ("es", "en"…), null for the Windows one. Stored as "UiLanguage": an older key held "es" for
    /// every installation and would otherwise force Spanish on everyone.
    /// </summary>
    [JsonPropertyName("UiLanguage")]
    public string? Language { get; set; }

    /// <summary>Profile on screen (Epic id or player name); null shows the most recent one.</summary>
    public string? Profile { get; set; }

    public string DemosFolder { get; set; } = "";

    /// <summary>Opens FCT when Fortnite starts (see FortniteWatch).</summary>
    public bool StartWithFortnite { get; set; } = true;

    [JsonPropertyName("OpenReportAtStartup")] // the file's key from older versions
    public bool OpenStatsOnStart { get; set; }

    /// <summary>"auto" installs new versions between matches, "prompt" offers them, "off" never asks GitHub.</summary>
    [JsonPropertyName("Updates")]
    public string UpdateMode { get; set; } = "auto";

    // ---- Overlay and live match -------------------------------------------------------------------------------------------

    public bool OverlayVisible { get; set; } = true;

    /// <summary>Click-through and fixed in place (the overlay's padlock).</summary>
    public bool OverlayLocked { get; set; }

    /// <summary>Position of the overlay window; null defaults to the top-left corner (0, 0).</summary>
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }

    /// <summary>Port of the OBS page (http://localhost:port/), served while FCT runs.</summary>
    public int ObsPort { get; set; } = ObsServer.DefaultPort;

    /// <summary>Reads the replay being recorded to count the match in progress.</summary>
    public bool LiveUpdate { get; set; } = true;

    /// <summary>Seconds between two readings of the replay in progress (never under 10).</summary>
    public int LiveIntervalSeconds { get; set; } = 30;

    // ---- What counts --------------------------------------------------------------------------------------------------------

    /// <summary>Extra container kinds added to the chest total (none by default).</summary>
    public bool CountAmmoBoxes { get; set; }
    public bool CountFoodBoxes { get; set; }
    public bool CountLootCannons { get; set; }

    /// <summary>Teammates in the window and on the page. Seen from the player's replay only, so their counts are minimums.</summary>
    public bool ShowTeammates { get; set; } = true;

    /// <summary>Adds what teammates were seen opening to the player's totals; only the figures change, never the history.</summary>
    public bool CountTeammateChests { get; set; }

    /// <summary>Hides matches before this local date from the figures (they stay in data.json). Null: every match.</summary>
    public DateTime? CountFrom { get; set; }

    /// <summary>The kinds chosen in <see cref="CountAmmoBoxes"/> and the like, as <see cref="ChestKinds"/> keys.</summary>
    public IReadOnlyCollection<string> ExtraKinds()
    {
        var kinds = new List<string>(3);
        void Add(bool on, string kind)
        {
            if (on)
            {
                kinds.Add(kind);
            }
        }
        Add(CountAmmoBoxes, ChestKinds.AmmoBox);
        Add(CountFoodBoxes, ChestKinds.FoodBox);
        Add(CountLootCannons, ChestKinds.Cannon);
        return kinds;
    }

    // ---- Chests before FCT ---------------------------------------------------------------------------------------------------

    /// <summary>Chests opened before using FCT, per profile key ("" before any profile exists). Only added to the total.</summary>
    public Dictionary<string, int> InitialChests { get; set; } = new();

    /// <summary>
    /// The estimate behind <see cref="InitialChests"/>, per profile key ("" when asked before the first match; it moves to
    /// that match's profile). It waits until there is an average to apply, then fills <see cref="InitialChests"/>.
    /// </summary>
    public Dictionary<string, HistoryEstimate> Estimates { get; set; } = new();

    /// <summary>Single, PC-wide estimate of older versions; <see cref="MoveLegacyEstimate"/> files it under its profile.</summary>
    public HistoryEstimate? Estimate { get; set; }

    /// <summary>Moves <see cref="Estimate"/> into <see cref="Estimates"/>. True when there was one to move.</summary>
    public bool MoveLegacyEstimate()
    {
        if (Estimate == null)
        {
            return false;
        }
        Estimates.TryAdd(Estimate.ProfileKey ?? Profile ?? "", Estimate);
        Estimate = null;
        return true;
    }

    // ---- Modes ------------------------------------------------------------------------------------------------------------

    /// <summary>Ranked Battle Royale counts too (off by default: only the modes of the event, see EventModes).</summary>
    public bool CountRanked { get; set; }

    /// <summary>
    /// Prizes of the weekly lists the player struck off (true) or brought back (false) by hand, by name. Without an entry a
    /// prize is struck off once obtained.
    /// </summary>
    public Dictionary<string, bool> PrizeMarks { get; set; } = new();

    /// <summary>The same settings for the stats page: the app and the page share every one of them.</summary>
    public Config ForStatsPage() => (Config)MemberwiseClone();

    // ---- Earlier versions ------------------------------------------------------------------------------------------------------

    /// <summary>Version that already cleared what earlier ones left (see <see cref="FCT.App.Leftovers"/>): once per version.</summary>
    public string? CleanedFor { get; set; }

    /// <summary>The exe that ran last: an older copy there goes away when a newer one opens.</summary>
    public string? LastExe { get; set; }

    // ---- Defaults that changed ------------------------------------------------------------------------------------------------

    /// <summary>Generation of defaults: 2 turned on start with Fortnite, the overlay and the teammates.</summary>
    public const int CurrentDefaults = 2;

    /// <summary>Generation this file already has; 0 for files older than the change. New files start at the current one.</summary>
    public int DefaultsVersion { get; set; }

    /// <summary>Turns the newer defaults on once for an older file; afterwards the player's choices stand. True if it did.</summary>
    public bool ApplyNewDefaults()
    {
        if (DefaultsVersion >= CurrentDefaults)
        {
            return false;
        }
        StartWithFortnite = OverlayVisible = ShowTeammates = true;
        DefaultsVersion = CurrentDefaults;
        return true;
    }
}

/// <summary>A player's season match count and how it is turned into chests before FCT (see HistoryEstimator).</summary>
public sealed class HistoryEstimate
{
    public int TotalMatches { get; set; }
    public DateTime SnapshotUtc { get; set; }

    /// <summary>New matches the average waits for (5 by default, 10 recommended).</summary>
    public int SampleSize { get; set; } = HistoryEstimator.DefaultSample;

    /// <summary>Chests per match typed by the player; null to use the measured average.</summary>
    public double? ManualAverage { get; set; }

    /// <summary>"api" (found on fortnite-api.com) or "manual" (typed).</summary>
    public string Source { get; set; } = "manual";

    /// <summary>Profile it belongs to; null means the one on screen when it is applied.</summary>
    public string? ProfileKey { get; set; }

    /// <summary>Already turned into <see cref="Config.InitialChests"/>.</summary>
    public bool Done { get; set; }
}
