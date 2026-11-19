using FCT.Analysis;

namespace FCT.Tests;

/// <summary>When an opponent's golden chest is known to the second and when it was already open when seen.</summary>
public class RivalTreasureTests
{
    private const string Chest = "Tiered_Chest_Athena_C_UAID_1";

    private static ReplaySignals With(params ReplaySignals.Search[] searches)
    {
        var signals = new ReplaySignals();
        signals.Searches.AddRange(searches);
        return signals;
    }

    private static ReplaySignals.Payout Payout(float at, string container = Chest) => new(at, "DelilahPrize:dpid_item18", 7, container);

    [Fact]
    public void ChestOpenedInRange_GivesTheExactTime()
    {
        var signals = With(new ReplaySignals.Search(442.9f, Chest, "Tiered_Chest_Athena_C", 7));
        Assert.False(signals.SeenLate(Payout(442.7f)));
    }

    [Fact]
    public void ChestAlreadyOpenWhenItCameIntoRange_IsSeenLate()
    {
        var signals = With(new ReplaySignals.Search(442.8f, Chest, "Tiered_Chest_Athena_C", 7, Stale: true));
        Assert.True(signals.SeenLate(Payout(442.7f)));
    }

    [Fact]
    public void AnotherChestOrALaterRefill_SaysNothing()
    {
        var signals = With(
            new ReplaySignals.Search(442.8f, "Tiered_Chest_Athena_C_UAID_2", "Tiered_Chest_Athena_C", 7, Stale: true),
            new ReplaySignals.Search(520f, Chest, "Tiered_Chest_Athena_C", 9, Stale: true));
        Assert.False(signals.SeenLate(Payout(442.7f)));
    }

    [Fact]
    public void UnknownContainer_KeepsTheTime()
    {
        Assert.False(With().SeenLate(Payout(100f, "")));
    }
}
