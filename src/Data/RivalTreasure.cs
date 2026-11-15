namespace FCT.Data;

/// <summary>
/// A featured treasure (cosmetic, Pirate Shop Access) an opponent got near the recording player: the golden chest it
/// opened was in the replay. Only what happened close by is recorded, and it never counts as the squad's.
/// </summary>
public sealed class RivalTreasure
{
    /// <summary>Server prize id, as for the squad's featured treasures ("DelilahPrize:dpid_item15", or a GUID).</summary>
    public string PrizeId { get; set; } = "";

    /// <summary>Seconds into the match.</summary>
    public float AtSeconds { get; set; }

    /// <summary>Name of the opponent who opened the chest (null when the replay does not tell).</summary>
    public string? Player { get; set; }

    /// <summary>The opponent was a bot.</summary>
    public bool Bot { get; set; }

    /// <summary>Kind of container the golden chest came out of (as for the squad's treasures; "" before analyzer 20).</summary>
    public string SourceKind { get; set; } = "";

    /// <summary>The container, as shown ("Cofre", "Cofre raro"…); unknown when the replay does not say.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SourceLabel => L.Name(ChestKinds.Shown(SourceKind.Length == 0 ? "unknown" : SourceKind));
}
