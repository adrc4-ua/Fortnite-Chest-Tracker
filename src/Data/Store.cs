namespace FCT.Data;

/// <summary>
/// The data folder (%APPDATA%\FCT by default): where each file lives, and loading/saving of the history, the settings
/// and the player's own prize names.
/// </summary>
public sealed class Store
{
    public Store(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
    }

    public string Folder { get; }

    public static string DefaultFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.ShortName);

    // ---- Files of the folder --------------------------------------------------------------------------------------------

    public string DataPath => InFolder("data.json");
    public string ConfigPath => InFolder("config.json");
    public string PrizeNamesPath => InFolder("treasures.json");
    public string StatsPath => InFolder("estadisticas.html");
    public string OverlayPath => InFolder("overlay.html");
    public string CsvPath => InFolder("matches.csv");
    public string ErrorLogPath => InFolder("errors.log");

    // ---- Backups (made when the stats are reset) ----------------------------------------------------------------------

    private const string BackupPrefix = "data-backup-", BackupStamp = "yyyyMMdd-HHmmss";

    /// <summary>Where a new backup of the history goes ("data-backup-20261114-180210.json").</summary>
    public string NewBackupPath() => InFolder($"{BackupPrefix}{DateTime.Now.ToString(BackupStamp, System.Globalization.CultureInfo.InvariantCulture)}.json");

    /// <summary>The chests from before FCT kept with a backup (per profile), next to it.</summary>
    public static string EarlierChestsOf(string backup) => Path.ChangeExtension(backup, null) + ".earlier.json";

    /// <summary>The backups of the history in the folder, newest first, with when each was made.</summary>
    public List<(string Path, DateTime Made)> Backups()
    {
        try
        {
            return Directory.EnumerateFiles(Folder, BackupPrefix + "*.json")
                .Where(path => !path.EndsWith(".earlier.json", StringComparison.OrdinalIgnoreCase))
                .Select(path => (path, Made: DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path)[BackupPrefix.Length..], BackupStamp,
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var made) ? made : File.GetLastWriteTime(path)))
                .OrderByDescending(backup => backup.Made)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new List<(string, DateTime)>();
        }
    }

    /// <summary>Earlier name of the stats page; removed once the current one has been written.</summary>
    public string LegacyStatsPath => InFolder("report.html");

    private string InFolder(string name) => Path.Combine(Folder, name);

    // ---- History and settings ------------------------------------------------------------------------------------------

    /// <summary>The saved history; a brand-new one is already at the current analyzer version (nothing to re-read).</summary>
    public TrackerData LoadData() =>
        JsonFile.Read<TrackerData>(DataPath) ?? new TrackerData { AnalyzerVersion = ReplayAnalyzer.CurrentVersion };

    public void SaveData(TrackerData data) => JsonFile.Write(DataPath, data);

    public Config? LoadConfig() => JsonFile.Read<Config>(ConfigPath);

    public void SaveConfig(Config config) => JsonFile.Write(ConfigPath, config);

    // ---- Prize names (treasures.json) ---------------------------------------------------------------------------------

    /// <summary>
    /// Loads the names the player gave to prizes (prize id → name) into <see cref="TreasureNames.PrizeNames"/>, creating
    /// an empty treasures.json the first time. Known prizes are named by <see cref="Prizes"/> and need no entry.
    /// </summary>
    public void LoadPrizeNames()
    {
        var saved = JsonFile.Read<Dictionary<string, string>>(PrizeNamesPath);
        if (saved == null)
        {
            saved = new Dictionary<string, string>();
            JsonFile.Write(PrizeNamesPath, saved);
        }
        TreasureNames.PrizeNames.Clear();
        foreach (var (id, name) in saved)
        {
            TreasureNames.PrizeNames[id] = name;
        }
    }

    /// <summary>
    /// Adds prize ids nobody has named yet to treasures.json, with an empty name for the player to fill in. Gizmos and
    /// catalogued prizes are skipped. Returns the ids that were added.
    /// </summary>
    public List<string> RegisterPrizeIds(IEnumerable<string> ids)
    {
        var unnamed = new List<string>();
        foreach (var id in ids)
        {
            var needsName = !TreasureNames.IsGizmoKey(id) && Prizes.ById(id) == null && !TreasureNames.PrizeNames.ContainsKey(id);
            if (needsName && !unnamed.Contains(id))
            {
                unnamed.Add(id);
            }
        }
        if (unnamed.Count > 0)
        {
            var saved = JsonFile.Read<Dictionary<string, string>>(PrizeNamesPath) ?? new Dictionary<string, string>();
            foreach (var id in unnamed)
            {
                saved[id] = "";
                TreasureNames.PrizeNames[id] = "";
            }
            JsonFile.Write(PrizeNamesPath, saved);
        }
        return unnamed;
    }
}
