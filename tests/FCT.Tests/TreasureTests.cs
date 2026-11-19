using FCT.Analysis;
using FCT.Data;
using FCT.Monitoring;
using FCT.PostMatch;

namespace FCT.Tests;

/// <summary>Prize table, the game's prize lists and the vote between captures of the end screen.</summary>
public class TreasureTests
{
    private const string Unknown = TreasureScreenReader.Unknown;

    [Fact]
    public void PrizeIds_AreOnePrizeEach_AndAnyGuidIsThePirateToken()
    {
        Assert.Equal("PPPP", Prizes.ById("DelilahPrize:dpid_item21")?.Name);
        Assert.Equal(Prizes.Kind.ShopAccess, Prizes.ById(Prizes.PirateShopId)?.Type);
        Assert.Equal(Prizes.Kind.ShopAccess, Prizes.ById(Guid.NewGuid().ToString())?.Type);
        Assert.Equal(Prizes.Kind.Gizmo, Prizes.ById("DelilahPrize:dpid_item9")?.Type);
        Assert.Null(Prizes.ById("DelilahPrize:dpid_item999"));
        Assert.Equal(Prizes.All.Count(p => p.Id != null), Prizes.All.Where(p => p.Id != null).Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Weeks_ChangeOnSundayAt14Utc()
    {
        Assert.Equal(2, Prizes.WeekOf(new DateTime(2026, 11, 15, 13, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(3, Prizes.WeekOf(new DateTime(2026, 11, 15, 14, 39, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ThreeStars_ForThePirateTokenAndOutfits()
    {
        Assert.Equal("★★★", Prizes.Stars(new TreasureFind { PrizeId = Prizes.PirateShopId, FromPrizeChest = true }));
        Assert.Equal("★★★", Prizes.Stars(new TreasureFind { PrizeId = "DelilahPrize:dpid_item19", FromPrizeChest = true }));
        Assert.Equal("★★", Prizes.Stars(new TreasureFind { PrizeId = "DelilahPrize:dpid_item21", FromPrizeChest = true }));
    }

    [Fact]
    public void HandMarks_CountOnlyPrizesNotFoundInMatches()
    {
        var match = Fakes.Match("a", "A", new DateTime(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc), 3);
        match.TreasureFinds.Add(new TreasureFind { PrizeId = "DelilahPrize:dpid_item21", FromPrizeChest = true });
        var detected = Prizes.DetectedPrizeNames(new[] { (match, match.TreasureFinds[0]) });
        var marks = new Dictionary<string, bool> { ["PPPP"] = true, ["Solid Gold Miku"] = true, ["London"] = false };
        Assert.Equal(1, Prizes.ManualMarksCount(marks, detected));
    }

    [Fact]
    public void PrizeLists_NewPrizeIsWonBetweenTwoLists_IncompleteListsAreSkipped()
    {
        var t = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var lists = new List<PrizeList>
        {
            new() { FirstSeenUtc = t, LastSeenUtc = t, Ids = new() { "DelilahPrize:dpid_item1" } },
            new() { FirstSeenUtc = t.AddHours(1), LastSeenUtc = t.AddHours(1), Ids = new() { "DelilahPrize:dpid_item7" } }, // incomplete
            new() { FirstSeenUtc = t.AddHours(2), LastSeenUtc = t.AddHours(2), Ids = new() { "DelilahPrize:dpid_item1", "DelilahPrize:dpid_item21" } },
        };
        var wins = PrizeListMerge.Wins(lists).ToList();
        Assert.Equal(new[] { ("DelilahPrize:dpid_item21", t, t.AddHours(2)) }, wins);
    }

    [Fact]
    public void PrizeLists_NameTheUnnamedTreasureOfTheStretch_AndUndoCleanly()
    {
        var t = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var data = new TrackerData();
        var match = Fakes.Match("a", "A", t.AddMinutes(10), 3);
        match.DurationSeconds = 1200;
        match.TreasureFinds.Add(new TreasureFind { Item = "?", ObtainedAtSeconds = 300 });
        data.Matches.Add(match);
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = t, LastSeenUtc = t, Ids = new() { "DelilahPrize:dpid_item1" } });
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = t.AddHours(1), LastSeenUtc = t.AddHours(1), Ids = new() { "DelilahPrize:dpid_item1", "DelilahPrize:dpid_item21" } });

        Assert.True(PrizeListMerge.Apply(data));
        var find = match.TreasureFinds.Single();
        Assert.Equal("DelilahPrize:dpid_item21", find.PrizeId);
        Assert.True(find.FromPrizeList);
        Assert.False(find.PrizeMatchLikely); // only one match in the stretch
        Assert.False(PrizeListMerge.Apply(data)); // same result again

        PrizeListMerge.Undo(data);
        Assert.Null(match.TreasureFinds.Single().PrizeId);
    }

    [Fact]
    public void PrizeList_SameListAgain_OnlyMovesItsTime()
    {
        var t = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var data = new TrackerData();
        Assert.True(PrizeListLog.FileList(data, t, t, new[] { "DelilahPrize:dpid_item1" }));
        Assert.False(PrizeListLog.FileList(data, t.AddHours(1), t.AddHours(1), new[] { "DelilahPrize:dpid_item1" }));
        Assert.Single(data.PrizeLists);
        Assert.Equal(t.AddHours(1), data.PrizeLists[0].LastSeenUtc);
        Assert.True(PrizeListLog.FileList(data, t.AddHours(2), t.AddHours(2), new[] { "DelilahPrize:dpid_item1", "DelilahPrize:dpid_item2" }));
        Assert.Equal(2, data.PrizeLists.Count);
    }

    [Fact]
    public void ScreenVote_ACardNeedsTwoCaptures()
    {
        var cards = TreasureScreenCapture.Combine(new List<List<string>>
        {
            new() { "Portable Extractor" },
            new() { "Portable Extractor", "Extraction Accelerator" },
            new() { "Portable Extractor", "Extraction Accelerator" },
            new() { "Portable Extractor", "Extraction Accelerator", "Golden Firework" }, // phantom: seen once
        });
        Assert.Equal(new[] { "Portable Extractor", "Extraction Accelerator" }, cards);
    }

    [Fact]
    public void ScreenVote_EarlyClearReadsAreNotOutvotedByLaterNoise()
    {
        // the player left the screen early: later captures read other UI
        var cards = TreasureScreenCapture.Combine(new List<List<string>>
        {
            new() { "Sprite Locator" },
            new() { "Sprite Locator" },
            new() { "Sprite Locator" },
            new() { Unknown },
            new() { "Lucky Locator" },
        });
        Assert.Equal(new[] { "Sprite Locator" }, cards);
    }

    [Fact]
    public void ScreenVote_OneStrayGizmoAmongUnknownsStaysUnknown()
    {
        var cards = TreasureScreenCapture.Combine(new List<List<string>>
        {
            new() { Unknown }, new() { Unknown }, new() { "Lucky Locator" }, new() { Unknown },
        });
        Assert.Equal(new[] { Unknown }, cards);
    }
}
