using FCT.Analysis;

namespace FCT.PostMatch;

/// <summary>
/// Tells the featured treasures apart from the gizmos when the replay has no golden chest for them (opened by a teammate
/// out of the replay's range): the game's log shows the server changing the player's account
/// (<see cref="TrackerData.AccountGrants"/>) in the same second the replay grants a treasure that adds a cosmetic, and
/// never for a gizmo (checked on real matches: about 30 gizmo grants without one, every featured grant with one, less
/// than half a second after it). Changes of the account at other moments (quests and the like) are not treasures and
/// are left alone. Which featured treasure it was is only known when a single one of the week was still missing.
/// </summary>
public static class AccountGrantMerge
{
    /// <summary>How long before and after the grant (replay time) the account change may be logged.</summary>
    private static readonly TimeSpan Before = TimeSpan.FromSeconds(1), After = TimeSpan.FromSeconds(3);

    /// <summary>Marks, in every match, the grants that came with an account change. True when any mark changed.</summary>
    public static bool Mark(TrackerData data)
    {
        var changed = false;
        foreach (var match in data.Matches)
        {
            changed |= Mark(data, match);
        }
        return changed;
    }

    /// <summary>Same for one match: each grant of the replay is marked again from the log.</summary>
    public static bool Mark(TrackerData data, MatchRecord match)
    {
        var changed = false;
        var start = match.RecordingStart();
        foreach (var find in match.TreasureFinds)
        {
            var mark = start is { } recording && find.FromPrizeHandler && !find.FromPrizeChest && find.ObtainedAtSeconds is { } at
                       && WithChange(data.AccountGrants, recording.AddSeconds(at));
            if (find.NewAccountItem != mark)
            {
                find.NewAccountItem = mark;
                changed = true;
            }
        }
        return changed;
    }

    private static bool WithChange(List<DateTime> changes, DateTime grantUtc) =>
        changes.Exists(c => DateTime.SpecifyKind(c, DateTimeKind.Utc) is var utc && utc >= grantUtc - Before && utc <= grantUtc + After);

    /// <summary>
    /// Names the marked treasures still unnamed when only one featured prize of their week (the Pirate Shop Access
    /// included) was not owned yet: owned in a prize list written before, or won in an earlier match (or earlier in the
    /// same one) whatever named it. Oldest first, so one named here counts as owned for the next.
    /// </summary>
    internal static void Deduce(TrackerData data)
    {
        var pending = data.Matches
            .SelectMany(m => m.TreasureFinds.Where(Unnamed).Select(f => (Match: m, Find: f, Won: WonAt(m, f))))
            .Where(p => p.Won != null)
            .OrderBy(p => p.Won)
            .ToList();
        foreach (var (match, find, won) in pending)
        {
            var week = Prizes.WeekOf(won!.Value);
            if (week == 0)
            {
                continue;
            }
            var missing = Prizes.All
                .Where(p => p.Id != null && (p.Type == Prizes.Kind.ShopAccess || (p.Type != Prizes.Kind.Gizmo && p.Week == week)))
                .Where(p => !Owned(data, match, find, won.Value, p.Id!))
                .ToList();
            if (missing.Count != 1)
            {
                continue;
            }
            var id = missing[0].Id!;
            find.GenericPrizeId = find.PrizeId;
            find.GenericItem = find.Item;
            find.PrizeId = id;
            find.Item = id;
            find.PrizeDeduced = true;
        }
    }

    /// <summary>How long after a match ends its "Treasure found!" screen can still come (the player may stay out of the lobby).</summary>
    private static readonly TimeSpan GiftWindow = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Names the featured treasures from the "Treasure found!" screens read in the lobby: each prize read belongs to the
    /// last match that ended before the screen (and not long before). In that match it names, in this order, the first
    /// grant that came with an account change and is still unnamed, or a featured card of the end screen the replay
    /// missed; if the match has neither, the replay missed the treasure and it is added. A prize the match already has
    /// (its golden chest was seen) needs nothing.
    /// </summary>
    internal static void NameFromGifts(TrackerData data)
    {
        foreach (var gift in data.GiftScreens.OrderBy(g => g.ShownUtc))
        {
            var shown = DateTime.SpecifyKind(gift.ShownUtc, DateTimeKind.Utc);
            var match = data.Matches
                .Select(m => (Match: m, End: EndOf(m)))
                .Where(x => x.End is { } end && end <= shown.AddMinutes(1) && end >= shown - GiftWindow)
                .OrderBy(x => x.End)
                .Select(x => x.Match)
                .LastOrDefault();
            if (match == null)
            {
                continue;
            }
            foreach (var id in gift.PrizeIds)
            {
                if (match.TreasureFinds.Any(f => string.Equals(f.PrizeId, id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                var find = match.TreasureFinds.Where(Unnamed).OrderBy(f => f.ObtainedAtSeconds ?? float.MaxValue).FirstOrDefault()
                           ?? match.TreasureFinds.FirstOrDefault(f => f.FromScreen && f.ScreenName == null && !f.FromGiftScreen);
                if (find != null)
                {
                    find.GenericPrizeId = find.PrizeId;
                    find.GenericItem = find.Item;
                    find.PrizeId = id;
                    find.Item = id;
                    find.FromGiftScreen = true;
                    continue;
                }
                match.TreasureFinds.Add(new TreasureFind
                {
                    Item = id,
                    RawItem = "GiftScreen",
                    PrizeId = id,
                    FromGiftScreen = true,
                    AddedFromGiftScreen = true,
                    SourceKind = "unknown",
                    SourceIsChest = true,
                    Finder = TreasureFind.FinderUnknown,
                });
            }
        }
    }

    /// <summary>When the match's replay ends (UTC).</summary>
    private static DateTime? EndOf(MatchRecord m) =>
        (m.RecordingStart() ?? (m.MatchStartUtc is DateTime s ? DateTime.SpecifyKind(s, DateTimeKind.Utc) : null)) is { } start
            ? start.AddSeconds(m.DurationSeconds ?? 0)
            : null;

    /// <summary>Undoes <see cref="Deduce"/> and <see cref="NameFromGifts"/> in one match.</summary>
    internal static void Undo(MatchRecord match)
    {
        match.TreasureFinds.RemoveAll(f => f.AddedFromGiftScreen);
        foreach (var find in match.TreasureFinds.Where(f => f.PrizeDeduced || f.FromGiftScreen))
        {
            find.PrizeId = find.GenericPrizeId;
            find.Item = find.GenericItem ?? TreasureNames.UnknownGizmo;
            find.PrizeDeduced = false;
            find.FromGiftScreen = false;
            find.GenericPrizeId = null;
            find.GenericItem = null;
        }
    }

    /// <summary>A featured treasure seen only through the account change: the replay never said which prize.</summary>
    internal static bool Unnamed(TreasureFind f) =>
        f.NewAccountItem && !f.FromPrizeList && !f.PrizeDeduced && !f.FromGiftScreen && (f.PrizeId == null || TreasureNames.IsGizmoKey(f.PrizeId));

    /// <summary>When the treasure was won (UTC), from the recording's start.</summary>
    internal static DateTime? WonAt(MatchRecord m, TreasureFind f) =>
        m.RecordingStart() is { } start && f.ObtainedAtSeconds is { } at ? start.AddSeconds(at) : null;

    private static bool Owned(TrackerData data, MatchRecord match, TreasureFind find, DateTime won, string id)
    {
        if (data.PrizeLists.Any(l => l.FirstSeenUtc < won && l.Ids.Contains(id, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }
        return data.Matches.Any(m => m.TreasureFinds.Any(f => !ReferenceEquals(f, find)
                                                              && string.Equals(f.PrizeId, id, StringComparison.OrdinalIgnoreCase)
                                                              && (WonAt(m, f) is { } other ? other < won : Earlier(m, match))));
    }

    /// <summary>The other match started first (for a find with no time of its own).</summary>
    private static bool Earlier(MatchRecord other, MatchRecord match) =>
        !ReferenceEquals(other, match) && other.MatchStartUtc is DateTime a && match.MatchStartUtc is DateTime b && a < b;
}
