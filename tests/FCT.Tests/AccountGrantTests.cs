using FCT.Analysis;
using FCT.Data;
using FCT.PostMatch;

namespace FCT.Tests;

/// <summary>
/// Featured treasures told apart from gizmos by the account change the game logs in the same second as the grant
/// (since update 43.11 the game no longer logs the list of owned prizes).
/// </summary>
public class AccountGrantTests
{
    // Real case (25 Nov 2026): replay recording from 00:47:13.593 UTC, grant at 472.2 s, account change at 00:55:05.721.
    private static readonly DateTime Recording = new(2026, 11, 25, 0, 47, 13, 593, DateTimeKind.Utc);
    private static readonly DateTime Change = new(2026, 11, 25, 0, 55, 5, 721, DateTimeKind.Utc);

    [Fact]
    public void LogLine_GivesTheMomentOfTheAccountChange_OnlyForTheAthenaProfileFromNotifications()
    {
        const string line = "[2026.11.25-00.55.05:721][177]LogProfileSys: MCP-Profile: Delta profile update (revision 9437 [14303]) for Skullbra accountId=MCP:0 profileId=athena from notifications";
        Assert.Equal(Change, PrizeListLog.ParseAccountLine(line));
        Assert.Null(PrizeListLog.ParseAccountLine(line.Replace("profileId=athena", "profileId=common_core")));
        Assert.Null(PrizeListLog.ParseAccountLine("[2026.11.25-01.03.39:348][831]LogProfileSys: MCP-Profile: Full profile update (rev=14309) for Skullbra profileId=athena."));
    }

    [Fact]
    public void Changes_AreFiledOnce()
    {
        var data = new TrackerData();
        Assert.True(PrizeListLog.FileGrants(data, new[] { Change }));
        Assert.False(PrizeListLog.FileGrants(data, new[] { Change }));
        Assert.Single(data.AccountGrants);
    }

    [Fact]
    public void GrantWithAccountChange_IsAFeaturedTreasure_AGrantWithoutOneStaysAGizmo()
    {
        var data = new TrackerData { AccountGrants = { Change } };
        var match = MatchWith(Grant(472.2f), Grant(120f));
        data.Matches.Add(match);

        Assert.True(PrizeListMerge.Apply(data));
        var featured = match.TreasureFinds.Single(f => f.ObtainedAtSeconds == 472.2f);
        Assert.True(featured.NewAccountItem);
        Assert.True(featured.Featured);
        Assert.Equal(Prizes.Featured, Prizes.Describe(featured).Name);
        Assert.False(match.TreasureFinds.Single(f => f.ObtainedAtSeconds == 120f).Featured);
        Assert.False(PrizeListMerge.Apply(data)); // same result again
    }

    [Fact]
    public void RecordingStart_ComesFromTheFileName_ForMatchesReadBeforeItWasKept()
    {
        var match = new MatchRecord { ReplayFile = "UnsavedReplay-2026.11.25-01.47.13.replay" };
        var expected = DateTime.SpecifyKind(new DateTime(2026, 11, 25, 1, 47, 13, DateTimeKind.Local).ToUniversalTime(), DateTimeKind.Utc);
        Assert.Equal(expected, match.RecordingStart());
        Assert.Null(new MatchRecord { ReplayFile = "replay.replay" }.RecordingStart());
    }

    [Fact]
    public void TheOnlyFeaturedPrizeOfTheWeekNotOwned_IsNamedByElimination()
    {
        // week 4: everything but the jam track (item26) already won in earlier matches
        var data = new TrackerData { AccountGrants = { Change } };
        var earlier = Fakes.Match("e", "A", Recording.AddDays(-1), 3);
        foreach (var id in new[] { "DelilahPrize:dpid_item24", "DelilahPrize:dpid_item25", "DelilahPrize:dpid_item27", "DelilahPrize:dpid_item28", Prizes.PirateShopId })
        {
            earlier.TreasureFinds.Add(new TreasureFind { PrizeId = id, FromPrizeChest = true });
        }
        var match = MatchWith(Grant(472.2f));
        data.Matches.AddRange(new[] { earlier, match });

        PrizeListMerge.Apply(data);
        var find = match.TreasureFinds.Single();
        Assert.Equal("DelilahPrize:dpid_item26", find.PrizeId);
        Assert.True(find.PrizeDeduced);
        Assert.Equal("Wellerman", Prizes.Describe(find).Name);

        PrizeListMerge.Undo(data);
        Assert.StartsWith(TreasureNames.UnknownPrefix, find.PrizeId);
        Assert.False(find.PrizeDeduced);
    }

    [Fact]
    public void SeveralFeaturedPrizesStillMissing_StaysAGenericFeaturedTreasure()
    {
        var data = new TrackerData { AccountGrants = { Change } };
        var match = MatchWith(Grant(472.2f));
        data.Matches.Add(match);

        PrizeListMerge.Apply(data);
        var find = match.TreasureFinds.Single();
        Assert.True(find.Featured);
        Assert.False(find.PrizeDeduced);
        Assert.StartsWith(TreasureNames.UnknownPrefix, find.PrizeId);
    }

    [Fact]
    public void PrizeList_NamesTheGrantWithAnAccountChange_AndTheMatchIsCertain()
    {
        // two matches in the stretch, both with a plain grant; only the second came with an account change
        var data = new TrackerData { AccountGrants = { Change } };
        var first = Fakes.Match("f", "A", Recording.AddHours(-1), 3);
        first.RecordingStartUtc = Recording.AddHours(-1);
        first.DurationSeconds = 1000;
        first.TreasureFinds.Add(Grant(300f));
        var second = MatchWith(Grant(472.2f));
        data.Matches.AddRange(new[] { first, second });
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = Recording.AddHours(-2), LastSeenUtc = Recording.AddHours(-2), Ids = new() { "DelilahPrize:dpid_item25" } });
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = Recording.AddHours(1), LastSeenUtc = Recording.AddHours(1), Ids = new() { "DelilahPrize:dpid_item25", "DelilahPrize:dpid_item26" } });

        PrizeListMerge.Apply(data);
        var named = second.TreasureFinds.Single();
        Assert.Equal("DelilahPrize:dpid_item26", named.PrizeId);
        Assert.False(named.PrizeMatchLikely);
        Assert.False(first.TreasureFinds.Single().Featured);
    }

    [Fact]
    public void ScreenMerge_TreatsTheGrantWithAnAccountChangeAsTheFeaturedCard()
    {
        // one "?" card (featured, unread) and one gizmo card: the gizmo card goes to the other grant
        var data = new TrackerData { AccountGrants = { Change } };
        var match = MatchWith(Grant(472.2f), Grant(600f));
        data.Matches.Add(match);
        data.TreasureScreens.Add(new TreasureScreen { ShownUtc = Recording.AddSeconds(1000), Cards = new() { TreasureScreenReader.Unknown, "Golden Firework" } });

        TreasureScreenMerge.ApplyAll(data);
        Assert.Null(match.TreasureFinds.Single(f => f.ObtainedAtSeconds == 472.2f).ScreenName);
        Assert.Equal("Golden Firework", match.TreasureFinds.Single(f => f.ObtainedAtSeconds == 600f).ScreenName);
    }

    [Fact]
    public void GiftScreen_NameOnScreen_GivesThePrize()
    {
        // what Windows read on the real screen (Spanish game): the bar of "2021 | 2:29" read as a 1
        var lines = new[] { "TESORO", "ENCONTRADO!", "PISTA DE IMPROVISACIÓN - 202112:29", "WELLERMAN", "RÉCOGER" };
        Assert.Equal("DelilahPrize:dpid_item26", GiftScreenReader.PrizeOf(lines, Change));
    }

    [Fact]
    public void GiftScreen_AnyLanguage_AJamTrackIsKnownByItsYearAndLength()
    {
        // a language whose words FCT does not know and a translated title: the "year | length" line still says jam track
        var lines = new[] { "SKARB ZNALEZIONY!", "ŚCIEŻKA - 2021 | 2:29", "SZANTA" };
        Assert.Equal("DelilahPrize:dpid_item26", GiftScreenReader.PrizeOf(lines, Change));
    }

    [Fact]
    public void GiftScreen_TypeWord_GivesTheWeeksPrizeOfThatType_NamesSplitOverLinesToo()
    {
        Assert.Equal("DelilahPrize:dpid_item25", GiftScreenReader.PrizeOf(new[] { "ACCESSOIRE DE DOS", "QUELQUE CHOSE" }, Change));
        Assert.Equal("DelilahPrize:dpid_item25", GiftScreenReader.PrizeOf(new[] { "MOCHILA", "SOLID GOLD HIGH TIDE AND", "WAVECUTTER" }, Change));
        Assert.Null(GiftScreenReader.PrizeOf(new[] { "ALGO", "QUE NO ES UN PREMIO" }, Change));
    }

    [Fact]
    public void GiftScreen_NamesTheFeaturedTreasureOfTheMatchThatJustEnded_AndUndoesCleanly()
    {
        var data = new TrackerData { AccountGrants = { Change } };
        var match = MatchWith(Grant(472.2f), Grant(120f));
        data.Matches.Add(match);
        // the screen comes back in the lobby, a few seconds after the replay ends
        data.GiftScreens.Add(new GiftScreen { ShownUtc = Recording.AddSeconds(982 + 30), PrizeIds = { "DelilahPrize:dpid_item26" } });

        Assert.True(PrizeListMerge.Apply(data));
        var named = match.TreasureFinds.Single(f => f.ObtainedAtSeconds == 472.2f);
        Assert.Equal("DelilahPrize:dpid_item26", named.PrizeId);
        Assert.True(named.FromGiftScreen);
        Assert.False(match.TreasureFinds.Single(f => f.ObtainedAtSeconds == 120f).Featured);
        Assert.Equal(2, match.TreasureFinds.Count);
        Assert.False(PrizeListMerge.Apply(data)); // same result again

        PrizeListMerge.Undo(data);
        Assert.StartsWith(TreasureNames.UnknownPrefix, named.PrizeId);
    }

    [Fact]
    public void GiftScreen_PrizeTheReplayDidNotSee_IsAdded_AndAMuchLaterScreenIsNotThisMatch()
    {
        var data = new TrackerData();
        var match = MatchWith(Grant(120f));
        data.Matches.Add(match);
        data.GiftScreens.Add(new GiftScreen { ShownUtc = Recording.AddSeconds(982 + 20), PrizeIds = { "DelilahPrize:dpid_item27" } });
        data.GiftScreens.Add(new GiftScreen { ShownUtc = Recording.AddHours(3), PrizeIds = { "DelilahPrize:dpid_item28" } });

        PrizeListMerge.Apply(data);
        Assert.Equal(2, match.TreasureFinds.Count);
        Assert.Contains(match.TreasureFinds, f => f.AddedFromGiftScreen && f.PrizeId == "DelilahPrize:dpid_item27");
        Assert.DoesNotContain(match.TreasureFinds, f => f.PrizeId == "DelilahPrize:dpid_item28");
    }

    private static TreasureFind Grant(float at) => new()
    {
        Item = TreasureNames.UnknownGizmo,
        PrizeId = $"{TreasureNames.UnknownPrefix}2026-11-25_01.46@{at}",
        ObtainedAtSeconds = at,
        TimeExact = true,
        FromPrizeHandler = true,
    };

    private static MatchRecord MatchWith(params TreasureFind[] finds)
    {
        var match = Fakes.Match("m", "A", Recording.AddSeconds(-57), 3);
        match.RecordingStartUtc = Recording;
        match.DurationSeconds = 982;
        match.TreasureFinds.AddRange(finds);
        return match;
    }
}
