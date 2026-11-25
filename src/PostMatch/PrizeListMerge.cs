using FCT.Analysis;

namespace FCT.PostMatch;

/// <summary>
/// Names the featured treasures the replays could not name, from the game's own list of the prizes the player owns
/// (<see cref="TrackerData.PrizeLists"/>). A prize that appears in the list was won between the previous list and
/// this one; its golden chest is only in the replay when it was opened near the player, so a teammate's far away comes
/// in as a plain treasure (or not at all, when the replay missed the treasure too). Among the matches of that stretch:
/// <list type="number">
/// <item>Already named by its golden chest in some match: nothing to do.</item>
/// <item>A match whose end screen is all gizmos (as many gizmo cards as its treasures) did not give it: left out.</item>
/// <item>A match whose end screen shows a card that is not a gizmo is the one, if it has an unnamed treasure.</item>
/// <item>Otherwise the latest unnamed treasure of the stretch; with none, the prize is added to the latest match.</item>
/// </list>
/// The match is certain when only one match of the stretch is left; otherwise it is marked likely. The treasure count
/// only changes when the replay missed the treasure. Before that, the grants that came with an account change are
/// marked as featured treasures (<see cref="AccountGrantMerge"/>; since update 43.11 the game writes no more lists, so
/// that is what tells them apart now), and those a list does not name are named by elimination when they can be.
/// Done again from scratch every time (undone first), so a new list, screen or reading always gives the same result.
/// </summary>
public static class PrizeListMerge
{
    /// <summary>Applies the lists to every match. True when anything changed.</summary>
    public static bool Apply(TrackerData data)
    {
        var before = Signature(data);
        Undo(data);
        AccountGrantMerge.Mark(data);
        AccountGrantMerge.NameFromGifts(data);
        foreach (var (id, after, before_) in Wins(data.PrizeLists))
        {
            Name(data, id, after, before_);
        }
        AccountGrantMerge.Deduce(data);
        return Signature(data) != before;
    }

    /// <summary>Undoes what an earlier pass did, so every pass starts from what the replays and screens gave.</summary>
    public static void Undo(TrackerData data)
    {
        foreach (var match in data.Matches)
        {
            Undo(match);
        }
    }

    /// <summary>Undoes it in one match (before that match's screen is applied again).</summary>
    public static void Undo(MatchRecord match)
    {
        AccountGrantMerge.Undo(match);
        match.TreasureFinds.RemoveAll(f => f.AddedFromPrizeList);
        foreach (var find in match.TreasureFinds.Where(f => f.FromPrizeList))
        {
            find.PrizeId = find.GenericPrizeId;
            find.Item = find.GenericItem ?? TreasureNames.UnknownGizmo;
            find.FromPrizeList = false;
            find.PrizeMatchLikely = false;
            find.GenericPrizeId = null;
            find.GenericItem = null;
        }
    }

    /// <summary>
    /// Each prize that appeared in a list, with the stretch it was won in: after the last list without it, before the
    /// first list with it. A list missing a prize an earlier list had is incomplete (prizes are never lost) and is not
    /// used. Prizes already in the first list were owned before: no stretch, nothing to name.
    /// </summary>
    internal static IEnumerable<(string Id, DateTime After, DateTime Before)> Wins(IReadOnlyList<PrizeList> lists)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTime? lastComplete = null;
        foreach (var list in lists.OrderBy(l => l.FirstSeenUtc))
        {
            if (!owned.All(id => list.Ids.Contains(id, StringComparer.OrdinalIgnoreCase)))
            {
                continue; // incomplete
            }
            if (lastComplete is { } after)
            {
                foreach (var id in list.Ids.Where(id => !owned.Contains(id)))
                {
                    yield return (id, after, list.FirstSeenUtc);
                }
            }
            owned.UnionWith(list.Ids);
            lastComplete = list.LastSeenUtc;
        }
    }

    private static void Name(TrackerData data, string id, DateTime after, DateTime before)
    {
        // named by its golden chest: done
        if (data.Matches.Any(m => m.TreasureFinds.Any(f => string.Equals(f.PrizeId, id, StringComparison.OrdinalIgnoreCase))))
        {
            return;
        }

        static DateTime Start(MatchRecord m) => DateTime.SpecifyKind(m.MatchStartUtc ?? m.AnalyzedAtUtc, DateTimeKind.Utc);
        static DateTime End(MatchRecord m) => Start(m).AddSeconds(m.DurationSeconds ?? 0);
        // matches played in the stretch, except those whose end screen shows only gizmos for all their treasures
        var matches = data.Matches
            .Where(m => m.MatchStartUtc != null && End(m) > after && Start(m) < before)
            .Where(m => !AllGizmos(data, m))
            .OrderBy(Start)
            .ToList();
        if (matches.Count == 0)
        {
            return; // played where FCT has no match (another PC, a replay not kept): it cannot be placed
        }

        bool Unnamed(MatchRecord m, TreasureFind f)
        {
            if ((f.Featured && !AccountGrantMerge.Unnamed(f)) || f.FromPrizeList || f.ScreenName != null)
            {
                return false;
            }
            // won inside the stretch (a treasure of a match that was going on when the list was written is not)
            return f.ObtainedAtSeconds is not { } at || (AccountGrantMerge.WonAt(m, f) ?? Start(m).AddSeconds(at)) is var won && won > after && won < before;
        }

        var withCard = matches.Where(m => HasFeaturedCard(data, m)).ToList();
        // a grant that came with an account change is a featured treasure for sure: the one, before any other (and the
        // match is certain when it is the only such grant of the stretch)
        var withChange = matches.SelectMany(m => m.TreasureFinds.Where(f => AccountGrantMerge.Unnamed(f) && Unnamed(m, f)).Select(f => (m, f))).ToList();
        var pick = withChange.LastOrDefault();
        if (pick.f == null)
        {
            pick = withCard.SelectMany(m => m.TreasureFinds.Where(f => Unnamed(m, f)).Select(f => (m, f))).LastOrDefault();
        }
        if (pick.f == null)
        {
            pick = matches.SelectMany(m => m.TreasureFinds.Where(f => Unnamed(m, f)).Select(f => (m, f))).LastOrDefault();
        }
        var likely = matches.Count > 1 && withChange.Count != 1;
        if (pick.f is { } find)
        {
            find.GenericPrizeId = find.PrizeId;
            find.GenericItem = find.Item;
            find.PrizeId = id;
            find.Item = id;
            find.FromPrizeList = true;
            find.PrizeMatchLikely = likely;
            return;
        }

        // the replay saw no treasure at all (a teammate far away while the player was out): added, to the match most
        // likely to have it
        var match = withCard.LastOrDefault() ?? matches[^1];
        match.TreasureFinds.Add(new TreasureFind
        {
            Item = id,
            RawItem = "PrizeList",
            PrizeId = id,
            FromPrizeList = true,
            AddedFromPrizeList = true,
            PrizeMatchLikely = likely,
            SourceKind = "unknown",
            SourceIsChest = true,
            Finder = TreasureFind.FinderUnknown,
        });
    }

    /// <summary>
    /// The match's end screen was read and shows only gizmos, at least one per treasure with a card: no featured prize
    /// (a featured one the player already owned has no card, and cannot be the new prize either).
    /// </summary>
    private static bool AllGizmos(TrackerData data, MatchRecord m) =>
        TreasureScreenMerge.ScreenOf(m, data.TreasureScreens) is { } screen && screen.Cards.Count > 0
        && screen.Cards.All(c => c != TreasureScreenReader.Unknown) && screen.Cards.Count >= OwnedPrizes.WithCard(data, m).Count;

    /// <summary>
    /// The match's end screen shows more cards that are not gizmos than the new featured treasures its replay already
    /// named (each of those has its card too; one the player already owned has none): one is a featured treasure the
    /// replay could not name.
    /// </summary>
    private static bool HasFeaturedCard(TrackerData data, MatchRecord m) =>
        TreasureScreenMerge.ScreenOf(m, data.TreasureScreens) is { } screen
        && screen.Cards.Count(c => c == TreasureScreenReader.Unknown) > OwnedPrizes.WithCard(data, m).Count(f => f.FromPrizeChest);

    private static string Signature(TrackerData data) =>
        string.Join("|", data.Matches.SelectMany(m => m.TreasureFinds.Where(f => f.FromPrizeList || f.NewAccountItem || f.PrizeDeduced || f.FromGiftScreen)
            .Select(f => $"{m.SessionId}:{f.PrizeId}:{f.PrizeMatchLikely}:{f.NewAccountItem}:{f.PrizeDeduced}:{f.FromGiftScreen}")));
}
