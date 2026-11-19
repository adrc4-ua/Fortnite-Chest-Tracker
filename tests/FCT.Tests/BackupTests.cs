using System.Text.Json;
using FCT.Analysis;
using FCT.Data;

namespace FCT.Tests;

/// <summary>A backup keeps the player's whole state, and restoring it brings exactly that state back.</summary>
public sealed class BackupTests : IDisposable
{
    private const string A = "11111111111111111111111111111111";
    private readonly Fakes _fakes = new();

    public void Dispose() => _fakes.Dispose();

    [Fact]
    public void Restore_BringsBackExactlyTheStateOfTheBackup_ButNotTheSettings()
    {
        var store = _fakes.Store;
        var replays = Path.Combine(_fakes.Folder, "Demos");
        Directory.CreateDirectory(replays);
        var config = _fakes.Config;
        config.DemosFolder = replays;
        var data = _fakes.Data;
        var tracker = new Tracker(store, config, data);

        // the state at backup time: matches with treasures, read replays, prize lists, earlier chests, a typed average,
        // a struck-off prize and a prize name
        var match = Fakes.Match(A, "A", DateTime.UtcNow.AddDays(-1), 12);
        match.TreasureFinds.Add(new TreasureFind());
        data.Matches.Add(match);
        data.Matches.Add(Fakes.Match(A, "A", DateTime.UtcNow.AddHours(-20), 7));
        data.ProcessedFiles["old.replay|1|1"] = match.SessionId;
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow, Ids = { "DelilahPrize:dpid_item21" } });
        config.InitialChests[A] = 900;
        config.Estimates[A] = new HistoryEstimate { TotalMatches = 90, SnapshotUtc = DateTime.UtcNow.AddDays(-2), ManualAverage = 10, Done = true, PlayerName = "A" };
        config.PrizeMarks["Pirate Shop Access"] = true;
        JsonFile.Write(store.PrizeNamesPath, new Dictionary<string, string> { ["DelilahPrize:dpid_x"] = "My prize" });
        config.Language = "es";
        store.SaveData(data);

        var backupPath = store.NewBackupPath();
        Backup.Write(backupPath, data, BackupState.Of(config, store));
        var dataThen = Json(data);
        var stateThen = Json(BackupState.Of(config, store));

        // everything changes afterwards: a new match (and its replay in the folder), other earlier chests, average,
        // marks and names, and a setting
        data.Matches.Add(Fakes.Match(A, "A", DateTime.UtcNow, 30));
        data.Matches[0].ChestsOpened = 99;
        data.PrizeLists.Clear();
        File.WriteAllText(Path.Combine(replays, "later.replay"), "not a real replay");
        config.InitialChests[A] = 5;
        config.Estimates[A].ManualAverage = 3;
        config.PrizeMarks.Clear();
        JsonFile.Write(store.PrizeNamesPath, new Dictionary<string, string>());
        config.Language = "en";

        var (backup, state) = Backup.Read(backupPath)!.Value;
        tracker.Restore(backup);
        state.ApplyTo(config);
        JsonFile.Write(store.PrizeNamesPath, state.PrizeNames);

        Assert.True(state.Complete);
        Assert.Equal(dataThen, Json(WithoutLaterReplay(data)));
        Assert.Equal(stateThen, Json(BackupState.Of(config, store)));
        Assert.Equal(dataThen, Json(WithoutLaterReplay(store.LoadData()))); // saved too
        Assert.Equal("en", config.Language); // a setting, not data: stays as it is now
        // the replay of the later match is left out, not counted by itself
        Assert.Equal("skipped", data.ProcessedFiles.Single(e => e.Key.StartsWith("later.replay|")).Value);
        Assert.Equal(1, tracker.SkippedOnDisk());
    }

    [Fact]
    public void ABackupOfAnEarlierVersion_RestoresItsHistoryAndChestsFromBefore()
    {
        var store = _fakes.Store;
        var old = Path.Combine(_fakes.Folder, "data-backup-20261101-120000.json");
        JsonFile.Write(old, new TrackerData { Matches = { Fakes.Match(A, "A", DateTime.UtcNow.AddDays(-3), 4) } });
        JsonFile.Write(Store.EarlierChestsOf(old), new Dictionary<string, int> { [A] = 300 });

        var (backup, state) = Backup.Read(old)!.Value;
        var config = new Config { InitialChests = { [A] = 1 }, PrizeMarks = { ["x"] = true } };
        state.ApplyTo(config);

        Assert.False(state.Complete);
        Assert.Single(backup.Matches);
        Assert.Equal(300, config.InitialChests[A]);
        Assert.True(config.PrizeMarks["x"]); // never kept by those backups: left as it is
    }

    [Fact]
    public void TheStateFile_IsNotListedAsABackup()
    {
        var store = _fakes.Store;
        var path = store.NewBackupPath();
        Backup.Write(path, new TrackerData { Matches = { Fakes.Match(A, "A", DateTime.UtcNow, 1) } }, new BackupState());
        Assert.Equal(path, Assert.Single(store.Backups()).Path);
    }

    private static TrackerData WithoutLaterReplay(TrackerData data)
    {
        var copy = JsonSerializer.Deserialize<TrackerData>(Json(data), JsonFile.Options)!;
        foreach (var key in copy.ProcessedFiles.Keys.Where(k => k.StartsWith("later.replay|")).ToList())
        {
            copy.ProcessedFiles.Remove(key);
        }
        return copy;
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonFile.Options);
}
