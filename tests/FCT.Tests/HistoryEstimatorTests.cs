using FCT.Analysis;
using FCT.Data;

namespace FCT.Tests;

public class HistoryEstimatorTests
{
    private static readonly DateTime Snapshot = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static List<MatchRecord> Before(int count, int chests) =>
        Enumerable.Range(1, count).Select(i => Fakes.Match("a", "A", Snapshot.AddHours(-i), chests)).ToList();

    private static List<MatchRecord> After(int count, int chests) =>
        Enumerable.Range(1, count).Select(i => Fakes.Match("a", "A", Snapshot.AddHours(i), chests)).ToList();

    [Fact]
    public void TypedAverage_IsUsedAtOnce_OverTheMatchesWithoutReplay()
    {
        var r = HistoryEstimator.Compute(Before(10, 3), 110, Snapshot, 5, 10.0);
        Assert.Equal(1000, r.Chests); // (110 − 10 replays) × 10
        Assert.Equal(100, r.Missing);
        Assert.Equal(0, r.Waiting);
    }

    [Fact]
    public void FifteenSavedMatches_GiveTheAverageAtOnce_WhateverSampleWasChosen()
    {
        var r = HistoryEstimator.Compute(Before(15, 4), 115, Snapshot, 10);
        Assert.Equal(400, r.Chests);
        Assert.Equal(4, r.Average);
    }

    [Fact]
    public void FewerSavedMatches_WaitForTheSample()
    {
        var r = HistoryEstimator.Compute(Before(3, 4).Concat(After(2, 6)).ToList(), 103, Snapshot, 5);
        Assert.Null(r.Chests);
        Assert.Equal(2, r.NewMatches);
        Assert.Equal(3, r.Waiting);
    }

    [Fact]
    public void SampleComplete_AveragesReplaysAndNewMatches()
    {
        var r = HistoryEstimator.Compute(Before(5, 4).Concat(After(5, 6)).ToList(), 105, Snapshot, 5);
        Assert.Equal(500, r.Chests); // average 5 over (105 − 5)
    }

    [Fact]
    public void SampleZero_WithSavedMatches_UsesThem()
    {
        var r = HistoryEstimator.Compute(Before(8, 5), 108, Snapshot, 0);
        Assert.Equal(500, r.Chests);
    }

    [Fact]
    public void SampleZero_WithoutAnyCountedMatch_WaitsInsteadOfGivingZero()
    {
        var r = HistoryEstimator.Compute(Array.Empty<MatchRecord>(), 100, Snapshot, 0);
        Assert.Null(r.Chests);
        Assert.Equal(HistoryEstimator.DefaultSample, r.Waiting);
    }

    // ---- Replays folder full (100 files) or not: every player has a different number ----------------------------

    /// <summary>Saved matches whose average is 4.6: rounded down it is 4.</summary>
    private static List<MatchRecord> AverageOf46(int count) =>
        Enumerable.Range(1, count).Select(i => Fakes.Match("a", "A", Snapshot.AddHours(-i), i % 5 < 3 ? 5 : 4)).ToList();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(250, true)]
    public void TheFolderIsCut_OnlyAtOneHundredReplays(int files, bool cut) => Assert.Equal(cut, HistoryEstimator.IsCut(files));

    [Fact]
    public void FullFolder_RoundsDown_EvenWhenFewerMatchesCount()
    {
        // 100 files in the folder, only 95 of them counted (other modes, the one being recorded…)
        var r = HistoryEstimator.Compute(AverageOf46(95), 200, Snapshot, 5, historyCut: true);
        Assert.True(r.RoundedDown);
        Assert.Equal(4, r.Average);
        Assert.Equal(420, r.Chests); // 4 × (200 − 95)
    }

    [Fact]
    public void FolderNotFull_KeepsTheExactAverage()
    {
        // a player with 40 replays: Fortnite deleted nothing, so the average is used as is
        var r = HistoryEstimator.Compute(AverageOf46(40), 100, Snapshot, 5, historyCut: false);
        Assert.False(r.RoundedDown);
        Assert.Equal(4.6, r.Average!.Value, 3);
        Assert.Equal(276, r.Chests); // 4.6 × 60
    }

    [Fact]
    public void NoReplaysAtAll_RoundsDown_TheAverageOfTheNewMatches()
    {
        var fresh = Enumerable.Range(1, 5).Select(i => Fakes.Match("a", "A", Snapshot.AddHours(i), i % 5 < 3 ? 5 : 4)).ToList();
        var r = HistoryEstimator.Compute(fresh, 50, Snapshot, 5, historyCut: false);
        Assert.True(r.RoundedDown);
        Assert.Equal(200, r.Chests); // 4 × 50
    }

    [Fact]
    public void TypedAverage_IsNeverRounded_WithAFullFolder()
    {
        var r = HistoryEstimator.Compute(AverageOf46(100), 150, Snapshot, 5, 7.5, historyCut: true);
        Assert.Equal(375, r.Chests); // 7.5 × 50
    }

    [Fact]
    public void OlderEstimates_WithoutTheFolderMark_JudgeByTheMatchesAsBefore()
    {
        Assert.True(HistoryEstimator.Compute(AverageOf46(100), 150, Snapshot, 5).RoundedDown);
        Assert.False(HistoryEstimator.Compute(AverageOf46(99), 150, Snapshot, 5).RoundedDown);
    }

    [Fact]
    public void OnlyThisSeasonsReplays_AreTakenOffTheTotal()
    {
        var old = Before(4, 4);
        foreach (var m in old)
        {
            m.GameVersion = "++Fortnite+Release-42.00";
        }
        old[0].GameVersion = Fakes.Version; // the newest is this season's
        var r = HistoryEstimator.Compute(old, 50, Snapshot, 5, 2.0);
        Assert.Equal(1, r.SeasonReplays);
        Assert.Equal(49, r.Missing);
    }
}
