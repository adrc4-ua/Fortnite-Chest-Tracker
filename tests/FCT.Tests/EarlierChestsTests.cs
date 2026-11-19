using FCT.Analysis;
using FCT.Data;
using FCT.Monitoring;

namespace FCT.Tests;

/// <summary>The estimate of the chests before FCT: where it is filed, and the Epic name against the replays'.</summary>
public class EarlierChestsTests
{
    private static readonly string A = Fakes.Account('a'), B = Fakes.Account('b');

    [Fact]
    public void FirstStart_WithName_IsFiledUnderTheNameOnly_AndShownAsTheProfile()
    {
        using var f = new Fakes();
        f.Show(null);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "Skullbra");

        Assert.Equal(new[] { "Skullbra" }, f.Config.Estimates.Keys);
        Assert.Equal(1000, Profiles.InitialChests(f.Config, "Skullbra"));
        Assert.False(f.Config.InitialChests.ContainsKey(""));
        Assert.Equal("Skullbra", Profiles.Current(f.Data, f.Config)?.Name);
    }

    // ---- Settings → Recalculate ----------------------------------------------------------------------------------------

    private static void Saved(Fakes f, int count, Func<int, int> chests)
    {
        for (var i = 1; i <= count; i++)
        {
            f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddDays(-1).AddMinutes(-i), chests(i)));
        }
    }

    [Theory]
    [InlineData(40, false)]  // few replays: Fortnite deleted nothing
    [InlineData(100, true)]  // full folder: the history is cut
    public void Recalculate_MarksWhetherTheFolderWasFull(int files, bool cut)
    {
        using var f = new Fakes { ReplayFiles = files };
        Saved(f, 20, i => i % 5 < 3 ? 5 : 4); // average 4.6
        f.Show(A);
        f.Earlier.SetEstimate(120, "api", 5, null, "Skullbra");

        var estimate = f.Config.Estimates[A];
        Assert.Equal(cut, estimate.HistoryCut);
        Assert.True(estimate.Done);
        Assert.Equal(cut ? 400 : 460, Profiles.InitialChests(f.Config, A)); // (4 or 4.6) × (120 − 20)
    }

    [Fact]
    public void Recalculate_CountingSkippedReplays_WaitsForThem_ThenUsesThem()
    {
        using var f = new Fakes { ReplayFiles = 100 };
        Saved(f, 16, _ => 3); // already counted, before the skipped ones are read
        f.Show(A);
        f.Earlier.SetEstimate(200, "api", 5, null, "Skullbra", replaysComing: true);

        // nothing computed from the incomplete matches
        Assert.False(f.Config.Estimates[A].Done);
        Assert.Equal(0, Profiles.InitialChests(f.Config, A));

        // the skipped replays are read: 64 more, same season
        Saved(f, 64, _ => 8);
        f.Show(A);
        f.Earlier.ApplyPending();

        Assert.True(f.Config.Estimates[A].Done);
        // average (16×3 + 64×8) / 80 = 7, over the 200 − 80 matches without replay
        Assert.Equal(840, Profiles.InitialChests(f.Config, A));
    }

    [Fact]
    public void FirstStart_WithoutReplays_WaitsForTheNewMatches()
    {
        using var f = new Fakes();
        f.Show(null);
        f.Earlier.SetEstimate(50, "api", 5, null, "Skullbra");
        Assert.False(f.Config.Estimates["Skullbra"].Done);
        Assert.Equal(false, f.Config.Estimates["Skullbra"].HistoryCut);

        for (var i = 1; i <= 5; i++)
        {
            f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddMinutes(i), i % 5 < 3 ? 5 : 4));
        }
        f.Earlier.ApplyPending();

        Assert.Equal(200, Profiles.InitialChests(f.Config, A)); // no replays: 4.6 rounded down to 4, × 50
    }

    [Fact]
    public void SetEstimate_NeverAsksTheStats()
    {
        // it runs on the window's thread: a lookup there could hang the window
        using var f = new Fakes();
        f.ApiAnswers(500);
        f.Data.Matches.Add(Fakes.Match(A, "OldName", DateTime.UtcNow.AddDays(-2), 5));
        f.Show(A);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "NewName");
        Assert.Equal(0, f.Lookups);
    }

    [Fact]
    public void ReplayWithSameName_MovesTheEstimateToTheAccount()
    {
        using var f = new Fakes();
        f.Show(null);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "Skullbra");
        f.Data.Matches.Add(Fakes.Match(A, "skullbra", DateTime.UtcNow.AddMinutes(5), 8));

        f.Earlier.ApplyPending();

        Assert.Equal(new[] { A }, f.Config.Estimates.Keys);
        Assert.Equal(A, f.Config.Profile);
        Assert.Equal(1000, Profiles.InitialChests(f.Config, A));
        Assert.Equal(0, f.Lookups); // same name: nothing to ask
    }

    [Fact]
    public void TypedName_FindsItsAccount_NotTheNewestOne()
    {
        using var f = new Fakes();
        f.Show(null);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "Skullbra");
        var t = DateTime.UtcNow.AddMinutes(1);
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", t, 5));
        f.Data.Matches.Add(Fakes.Match(B, "Omega_x267", t.AddMinutes(30), 5));

        f.Earlier.ApplyPending();

        Assert.Equal(new[] { A }, f.Config.Estimates.Keys);
        Assert.Equal(0, Profiles.InitialChests(f.Config, B));
        Assert.Null(Profiles.EstimateOf(f.Config, B));
    }

    [Fact]
    public void WrongName_StatsFound_ReplaysNameWinsAndEstimateIsRedone()
    {
        using var f = new Fakes();
        f.ApiAnswers(302);
        f.Show(null);
        f.Earlier.SetEstimate(100, "manual", 5, 10.0, "Skulbra");
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddMinutes(1), 5));

        f.Earlier.ApplyPending();

        var estimate = f.Config.Estimates[A];
        Assert.Equal("Skullbra", estimate.PlayerName);
        Assert.Equal("api", estimate.Source);
        // the match after the estimate is a new match: taken off today's total, the progress is kept
        Assert.Equal(301, estimate.TotalMatches);
        Assert.Equal(3010, Profiles.InitialChests(f.Config, A));
        Assert.Equal(1, f.Lookups);

        f.Earlier.ApplyPending();
        Assert.Equal(1, f.Lookups); // asked once, not every round
    }

    [Fact]
    public void WrongName_StatsNotFound_FiguresStayAsTyped()
    {
        using var f = new Fakes();
        f.Show(null);
        f.Earlier.SetEstimate(100, "manual", 5, 10.0, "Skulbra");
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddMinutes(1), 5));

        f.Earlier.ApplyPending();

        var estimate = f.Config.Estimates[A];
        Assert.Equal("Skullbra", estimate.PlayerName);
        Assert.Equal(100, estimate.TotalMatches);
        Assert.Equal("manual", estimate.Source);
        Assert.Equal(1000, Profiles.InitialChests(f.Config, A));
    }

    [Fact]
    public void WrongName_StatsPrivate_SettlesWithTheReplaysName_FiguresAsTyped()
    {
        using var f = new Fakes();
        f.ApiFails(FCT.App.StatsClient.Outcome.Private);
        f.Show(null);
        f.Earlier.SetEstimate(100, "manual", 5, 10.0, "Skulbra");
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddMinutes(1), 5));

        f.Earlier.ApplyPending();
        f.Earlier.ApplyPending();

        Assert.Equal("Skullbra", f.Config.Estimates[A].PlayerName);
        Assert.Equal(100, f.Config.Estimates[A].TotalMatches);
        Assert.Equal(1, f.Lookups); // a definite answer: never asked again
    }

    [Fact]
    public void WrongName_NoConnection_KeepsFiguresAndAsksAgainLater()
    {
        using var f = new Fakes();
        EarlierChests.RetryAfter = TimeSpan.FromMilliseconds(300); // 5 minutes in the app
        f.ApiFails(FCT.App.StatsClient.Outcome.Failed);
        f.Show(null);
        f.Earlier.SetEstimate(100, "manual", 5, 10.0, "Skulbra");
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddMinutes(1), 5));

        f.Earlier.ApplyPending();
        Assert.Equal(1, f.Lookups);
        Assert.True(f.Config.Estimates.ContainsKey(A)); // moved to the account already
        Assert.Equal("Skulbra", f.Config.Estimates[A].PlayerName); // not settled: asked again later
        Assert.Equal(1000, Profiles.InitialChests(f.Config, A));

        f.Earlier.ApplyPending();
        f.Earlier.ApplyPending();
        Assert.Equal(1, f.Lookups); // not every round: only once the wait is over

        f.ApiAnswers(302); // the connection is back
        Thread.Sleep(400);
        f.Earlier.ApplyPending();
        Assert.Equal(2, f.Lookups);
        Assert.Equal("Skullbra", f.Config.Estimates[A].PlayerName);
        Assert.Equal(301, f.Config.Estimates[A].TotalMatches);
    }

    [Fact]
    public void OtherProfileOnPurpose_IsNeverCheckedAgainstTheReplays()
    {
        using var f = new Fakes();
        f.ApiAnswers(999);
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddDays(-1), 5));
        f.Show(A);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "Omega_x267", otherProfile: true);

        f.Earlier.ApplyPending();

        Assert.Equal(0, f.Lookups);
        Assert.Equal("Omega_x267", f.Config.Estimates[A].PlayerName);
        Assert.Equal(100, f.Config.Estimates[A].TotalMatches);
    }

    [Fact]
    public void RenamedAccount_ComparesWithTheNewestName()
    {
        // the oldest replays carry the old name: typing the current one is not a mismatch
        using var f = new Fakes();
        f.ApiAnswers(999);
        f.Data.Matches.Add(Fakes.Match(A, "OldName", DateTime.UtcNow.AddDays(-3), 5));
        f.Data.Matches.Add(Fakes.Match(A, "NewName", DateTime.UtcNow.AddDays(-1), 5));
        f.Show(A);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "NewName");

        f.Earlier.ApplyPending();

        Assert.Equal(0, f.Lookups);
        Assert.Equal(100, f.Config.Estimates[A].TotalMatches);
    }

    [Fact]
    public void PendingEstimate_IsAppliedOnce_WithOneNotice()
    {
        using var f = new Fakes();
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddDays(-1), 4));
        f.Show(A);
        f.Earlier.SetEstimate(51, "api", 1, null, "Skullbra");
        Assert.False(f.Config.Estimates[A].Done);

        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddMinutes(1), 6));
        var notices = f.Earlier.ApplyPending();

        Assert.Single(notices);
        Assert.True(f.Config.Estimates[A].Done);
        Assert.Equal(250, Profiles.InitialChests(f.Config, A)); // average 5 × (51 − 1)
        Assert.Empty(f.Earlier.ApplyPending());
    }

    [Fact]
    public void AskedBeforeAnyMatchWithoutName_IsKeptUnderEmptyKey()
    {
        // without a name nor a profile it waits under "" (Tracker.AdoptFirstProfile hands it to the first match's)
        using var f = new Fakes();
        f.Show(null);
        f.Earlier.SetEstimate(40, "manual", 5, 2.0);

        Assert.Equal(new[] { "" }, f.Config.Estimates.Keys);
        Assert.Equal(80, Profiles.InitialChests(f.Config, null));
        Assert.Null(Profiles.Current(f.Data, f.Config));
    }

    [Fact]
    public void OldCopyUnderEmptyKey_IsDropped_AndNeverShownOnAnotherProfile()
    {
        // 5.12.x filed each estimate twice: under its account and under ""
        using var f = new Fakes();
        var snapshot = DateTime.UtcNow.AddDays(-1);
        f.Config.Estimates[A] = new HistoryEstimate { TotalMatches = 50, SnapshotUtc = snapshot, Done = true, ManualAverage = 7, PlayerName = "A1", ProfileKey = A };
        f.Config.Estimates[""] = new HistoryEstimate { TotalMatches = 50, SnapshotUtc = snapshot, Done = true, ManualAverage = 7 };
        f.Config.InitialChests[A] = 350;
        f.Config.InitialChests[""] = 350;
        f.Data.Matches.Add(Fakes.Match(A, "A1", snapshot.AddDays(-2), 5));
        f.Data.Matches.Add(Fakes.Match(B, "B1", snapshot.AddDays(-2), 5));

        Assert.Equal(0, Profiles.InitialChests(f.Config, B)); // even before the clean-up
        f.Earlier.ApplyPending();

        Assert.False(f.Config.Estimates.ContainsKey(""));
        Assert.False(f.Config.InitialChests.ContainsKey(""));
        Assert.Equal(350, Profiles.InitialChests(f.Config, A));
        Assert.Null(Profiles.EstimateOf(f.Config, B));
    }

    [Fact]
    public void OnlySavedMatches_RemovesTheEstimateAndTheNumber()
    {
        using var f = new Fakes();
        f.Data.Matches.Add(Fakes.Match(A, "Skullbra", DateTime.UtcNow.AddDays(-1), 4));
        f.Show(A);
        f.Earlier.SetEstimate(100, "api", 5, 10.0, "Skullbra");
        f.Earlier.SetEstimate(null, "api", 5, null);
        Assert.Empty(f.Config.Estimates);
        Assert.Equal(0, Profiles.InitialChests(f.Config, A));
    }
}
