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
    public void ScreenReader_FindsTheCardsOfThePanel()
    {
        // two settled cards at their place on a 1920×1080 end screen (measured on real captures)
        var frame = Screen((112, 380, 100, 92), (240, 380, 104, 100));
        Assert.Equal(new[] { 112, 240 }, TreasureScreenReader.Read(frame).Select(c => c.X));
    }

    [Fact]
    public void ScreenReader_IgnoresTheGoldArtOfTheScreensThatFollow()
    {
        // a yellow Sprite of the collection (narrow and tall), a gold blob of a loading screen away from the panel's
        // margin, and one of the lobby (smaller than a card): each stayed for many captures in a row
        Assert.Empty(TreasureScreenReader.Read(Screen((80, 216, 84, 108))));
        Assert.Empty(TreasureScreenReader.Read(Screen((564, 372, 100, 92))));
        Assert.Empty(TreasureScreenReader.Read(Screen((112, 380, 88, 84))));
    }

    [Fact]
    public void ScreenMerge_MissedGizmoIsNamed_PastTheFeaturedCardTheReplaySaw()
    {
        // real match: a teammate's back bling seen by the replay, the player's Sprite Locator missed by it; the screen
        // shows the Sprite Locator first
        var match = ScreenMatch(new TreasureFind { PrizeId = "DelilahPrize:dpid_item20", FromPrizeChest = true, ObtainedAtSeconds = 990 });
        var screen = new TreasureScreen { ShownUtc = ScreenStart.AddSeconds(1000), Cards = new() { "Sprite Locator", Unknown } };

        Assert.True(TreasureScreenMerge.Apply(match, new[] { screen }));
        var added = match.TreasureFinds.Single(f => f.FromScreen);
        Assert.Equal("Sprite Locator", added.ScreenName);
        Assert.Equal(2, match.TreasureFinds.Count);
    }

    [Fact]
    public void ScreenMerge_GizmosAreNamed_WhenTheFeaturedCardWasNotRead()
    {
        // real match: two gizmos and a loading screen; the captures only read the two gizmo cards
        var match = ScreenMatch(
            new TreasureFind { Item = "Treasure gizmo", ObtainedAtSeconds = 203 },
            new TreasureFind { Item = "Treasure gizmo", ObtainedAtSeconds = 328 },
            new TreasureFind { PrizeId = "DelilahPrize:dpid_item22", FromPrizeChest = true, ObtainedAtSeconds = 356 });
        var screen = new TreasureScreen { ShownUtc = ScreenStart.AddSeconds(900), Cards = new() { "Portable Extractor", "Extraction Accelerator" } };

        Assert.True(TreasureScreenMerge.Apply(match, new[] { screen }));
        Assert.Equal(new[] { "Portable Extractor", "Extraction Accelerator", null },
            match.TreasureFinds.OrderBy(f => f.ObtainedAtSeconds).Select(f => f.ScreenName));
        Assert.DoesNotContain(match.TreasureFinds, f => f.FromScreen);
    }

    [Fact]
    public void ScreenMerge_AnOwnedFeaturedHasNoCard_SoTheMissedGizmoIsStillAdded()
    {
        // the player opened a loading screen they already owned (real: no grant, no card), and a teammate far away won
        // a Sprite Locator the replay did not see: the only card is the Sprite Locator
        var data = new TrackerData();
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = ScreenStart.AddDays(-1), LastSeenUtc = ScreenStart.AddDays(-1), Ids = new() { "DelilahPrize:dpid_item22" } });
        var match = ScreenMatch(new TreasureFind { PrizeId = "DelilahPrize:dpid_item22", FromPrizeChest = true, ObtainedAtSeconds = 356 });
        data.Matches.Add(match);
        data.TreasureScreens.Add(new TreasureScreen { ShownUtc = ScreenStart.AddSeconds(1000), Cards = new() { "Sprite Locator" } });

        Assert.True(TreasureScreenMerge.ApplyAll(data));
        Assert.Equal("Sprite Locator", match.TreasureFinds.Single(f => f.FromScreen).ScreenName);
        Assert.Equal(2, match.TreasureFinds.Count);
    }

    [Fact]
    public void PrizeLists_FeaturedCardBesideAnOwnedFeatured_PointsToItsMatch()
    {
        // match A: the player reopened a prize already owned (golden chest, no card) and a teammate far away won a new
        // one (plain grant, "?" card); match B, later in the same stretch, has an unnamed treasure and no screen
        var data = new TrackerData();
        var t = ScreenStart;
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = t.AddHours(-1), LastSeenUtc = t.AddHours(-1), Ids = new() { "DelilahPrize:dpid_item22" } });
        data.PrizeLists.Add(new PrizeList { FirstSeenUtc = t.AddHours(3), LastSeenUtc = t.AddHours(3), Ids = new() { "DelilahPrize:dpid_item20", "DelilahPrize:dpid_item22" } });
        var a = ScreenMatch(
            new TreasureFind { PrizeId = "DelilahPrize:dpid_item22", FromPrizeChest = true, ObtainedAtSeconds = 300 },
            new TreasureFind { Item = "Treasure gizmo", ObtainedAtSeconds = 600 });
        var b = Fakes.Match("a", "A", t.AddHours(1), 3);
        b.DurationSeconds = 1000;
        b.TreasureFinds.Add(new TreasureFind { Item = "Treasure gizmo", ObtainedAtSeconds = 200 });
        data.Matches.AddRange(new[] { a, b });
        data.TreasureScreens.Add(new TreasureScreen { ShownUtc = t.AddSeconds(1000), Cards = new() { Unknown } });

        TreasureScreenMerge.ApplyAll(data);
        Assert.True(PrizeListMerge.Apply(data));
        Assert.Equal("DelilahPrize:dpid_item20", a.TreasureFinds.Single(f => f.FromPrizeList).PrizeId);
        Assert.DoesNotContain(b.TreasureFinds, f => f.FromPrizeList);
    }

    private static readonly DateTime ScreenStart = new(2026, 11, 15, 16, 9, 0, DateTimeKind.Utc);

    private static MatchRecord ScreenMatch(params TreasureFind[] finds)
    {
        var match = Fakes.Match("a", "A", ScreenStart, 3);
        match.DurationSeconds = 1000;
        match.TreasureFinds.AddRange(finds);
        return match;
    }

    /// <summary>A dark blue 1920×1080 screen with gold boxes (the colour of a treasure card) at these places.</summary>
    private static WindowCapture.Frame Screen(params (int X, int Y, int W, int H)[] boxes)
    {
        const int width = 1920, height = 1080;
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]) = ((byte)120, (byte)30, (byte)20, (byte)255);
        }
        foreach (var (x0, y0, w, h) in boxes)
        {
            for (var y = y0; y < y0 + h; y++)
            {
                for (var x = x0; x < x0 + w; x++)
                {
                    var i = (y * width + x) * 4;
                    (bgra[i], bgra[i + 1], bgra[i + 2]) = ((byte)60, (byte)190, (byte)240);
                }
            }
        }
        return new WindowCapture.Frame(width, height, bgra);
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
