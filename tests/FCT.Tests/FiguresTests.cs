using FCT.Analysis;
using FCT.Data;
using FCT.Localization;
using FCT.Monitoring;
using FCT.Overlay;

namespace FCT.Tests;

/// <summary>Average per match, overlay columns and estimate progress.</summary>
public class FiguresTests
{
    private static readonly string A = Fakes.Account('a'), B = Fakes.Account('b');

    private static readonly DateTime Snapshot = DateTime.UtcNow.AddDays(-1);

    /// <summary>"I know it, I'll type it": 10 chests per match over a season of 100 matches.</summary>
    private static HistoryEstimate Typed(double average = 10.0, int total = 100) =>
        new() { TotalMatches = total, SnapshotUtc = Snapshot, ManualAverage = average, Done = true };

    [Fact]
    public void Average_WithoutMatches_IsTheTypedOne()
    {
        var stats = ScreenFigures.Stats(new List<MatchRecord>(), null, 1000, 100, Typed());
        Assert.Equal(10.0, stats.Average);
    }

    [Fact]
    public void Average_RightAfterTheEstimate_IsTheTypedOne_EvenWithSavedReplays()
    {
        // 30 saved replays (before the estimate) with 4 chests each: the typed 10 is still what shows
        var saved = Enumerable.Range(1, 30).Select(i => Fakes.Match(A, "A", Snapshot.AddHours(-i), 4)).ToList();
        var stats = ScreenFigures.Stats(saved, null, 700, 70, Typed());
        Assert.Equal(10.0, stats.Average);
    }

    [Fact]
    public void Average_GrowsFromTheTypedBase()
    {
        // (10 × 100 base + 30 new) / (100 base + 2 new)
        var matches = new List<MatchRecord> { Fakes.Match(A, "A", Snapshot.AddHours(1), 10), Fakes.Match(A, "A", Snapshot.AddHours(2), 20) };
        var stats = ScreenFigures.Stats(matches, null, 1000, 100, Typed());
        Assert.Equal(1030.0 / 102, stats.Average, 6);
    }

    [Fact]
    public void Average_WithACalculatedEstimate_IsChestsOverMatches()
    {
        var calculated = new HistoryEstimate { TotalMatches = 100, SnapshotUtc = Snapshot, Done = true };
        var matches = new List<MatchRecord> { Fakes.Match(A, "A", Snapshot.AddHours(1), 10), Fakes.Match(A, "A", Snapshot.AddHours(2), 20) };
        var stats = ScreenFigures.Stats(matches, null, 800, 100, calculated);
        Assert.Equal(830.0 / 102, stats.Average, 6);
    }

    [Fact]
    public void Overlay_WithoutMatchesToday_ShowsTheTypedBaseMovedByTheMatchesSince()
    {
        // typed 10 over 10 matches, then 2 matches of 22: (100 + 44) / 12 = 12
        var config = new Config();
        config.Estimates[A] = Typed(10, 10);
        var data = new TrackerData();
        data.Matches.Add(Fakes.Match(A, "A", Snapshot.AddHours(1), 22));
        data.Matches.Add(Fakes.Match(A, "A", Snapshot.AddHours(2), 22));
        var view = OverlayModel.Build(data, config, A);
        if (data.Matches.Any(m => m.MatchStartUtc!.Value.ToLocalTime().Date == DateTime.Now.Date))
        {
            return; // played "today" when run just after midnight: the column shows today's average then
        }
        Assert.Equal("12", view.Rows.Single(r => r.Label == L.T("Media", "Avg.")).Value);
    }

    [Fact]
    public void Average_WithoutEstimate_IsTheMatchesOwn()
    {
        var matches = new List<MatchRecord> { Fakes.Match(A, "A", DateTime.UtcNow.AddHours(-2), 4), Fakes.Match(A, "A", DateTime.UtcNow.AddHours(-1), 6) };
        Assert.Equal(5.0, ScreenFigures.Stats(matches, null).Average);
    }

    [Fact]
    public void Overlay_TotalAddsEarlierChests_AndAverageColumnUsesTypedOneOnlyWithoutMatchesToday()
    {
        var data = new TrackerData();
        data.Matches.Add(Fakes.Match(A, "A", DateTime.UtcNow.AddDays(-2), 7));
        var view = OverlayModel.Build(data, 100, null, 9.4);
        Assert.Equal(L.N(107), view.Total);
        Assert.Equal("9", view.Rows.Single(r => r.Label == L.T("Media", "Avg.")).Value);
    }

    [Fact]
    public void Overlay_UsesOnlyTheShownProfilesTypedAverage()
    {
        var config = new Config();
        config.Estimates[A] = new HistoryEstimate { TotalMatches = 10, SnapshotUtc = DateTime.UtcNow, Done = true, ManualAverage = 12 };
        var data = new TrackerData();
        data.Matches.Add(Fakes.Match(B, "B", DateTime.UtcNow.AddDays(-2), 7));
        config.Profile = B;

        var view = OverlayModel.Build(data, config, B);

        Assert.NotEqual("12", view.Rows.Single(r => r.Label == L.T("Media", "Avg.")).Value);
    }

    [Fact]
    public void EstimationProgress_CountsTheNewMatchesOfTheProfile()
    {
        var config = new Config();
        var snapshot = DateTime.UtcNow.AddDays(-1);
        config.Estimates[A] = new HistoryEstimate { TotalMatches = 100, SnapshotUtc = snapshot, SampleSize = 5 };
        var view = new TrackerData();
        view.Matches.Add(Fakes.Match(A, "A", snapshot.AddHours(1), 3));
        view.Matches.Add(Fakes.Match(A, "A", snapshot.AddHours(2), 3));

        Assert.Equal("2/5", OverlayModel.EstimationProgress(view, config, A));

        config.Estimates[A].Done = true;
        Assert.Null(OverlayModel.EstimationProgress(view, config, A));
    }

    [Fact]
    public void InitialChests_AreTheAccountsOwn()
    {
        var config = new Config();
        config.InitialChests[A] = 500;
        config.InitialChests[""] = 40;
        Assert.Equal(500, Profiles.InitialChests(config, A));
        Assert.Equal(0, Profiles.InitialChests(config, B));
        Assert.Equal(40, Profiles.InitialChests(config, null));
    }
}
