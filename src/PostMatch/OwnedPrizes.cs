namespace FCT.PostMatch;

/// <summary>
/// Featured prizes the player already owned when a match started. Winning one again gives the player nothing new, so
/// it leaves no grant in the player's replay, no card on the end screen (measured on real matches: no screen at all
/// when it was the only treasure) and no change in the game's prize list; only its golden chest is in the replay,
/// when it was opened near the player. Matching the end screen's cards must leave those finds out. One won by a
/// teammate far away leaves no trace at all on this PC.
/// </summary>
internal static class OwnedPrizes
{
    /// <summary>
    /// True when <paramref name="prizeId"/> was owned before <paramref name="match"/> started: it is in a prize list the
    /// game wrote before, or an earlier match's golden chest gave it (the squad shares what a teammate wins).
    /// </summary>
    public static bool Before(TrackerData data, MatchRecord match, string? prizeId)
    {
        if (prizeId == null || match.MatchStartUtc is not DateTime start)
        {
            return false;
        }
        var begin = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        if (data.PrizeLists.Any(l => l.FirstSeenUtc < begin && l.Ids.Contains(prizeId, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }
        return data.Matches.Any(m => !ReferenceEquals(m, match) && m.MatchStartUtc is DateTime other
                                     && DateTime.SpecifyKind(other, DateTimeKind.Utc) < begin
                                     && m.TreasureFinds.Any(f => f.FromPrizeChest && string.Equals(f.PrizeId, prizeId, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The finds of this match that have a card on its end screen: all but the featured ones already owned.</summary>
    public static List<TreasureFind> WithCard(TrackerData? data, MatchRecord match) =>
        data == null
            ? match.TreasureFinds.ToList()
            : match.TreasureFinds.Where(f => !(f.Featured && Before(data, match, f.PrizeId))).ToList();
}
