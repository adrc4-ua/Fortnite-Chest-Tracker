namespace FCT.Overlay;

/// <summary>
/// Everything the overlay draws, already formatted (so the same view feeds the game overlay, the OBS page and the
/// previews).
/// </summary>
/// <param name="Total">The big number: chests in total.</param>
/// <param name="Current">The second number (last match); null draws a dash.</param>
/// <param name="CurrentCaption">Caption under <paramref name="Current"/>.</param>
/// <param name="Rows">The figures under the numbers.</param>
/// <param name="Estimation">Progress of an active estimation waiting for played matches ("2/5", or null).</param>
public sealed record OverlayView(string Total, string? Current, string CurrentCaption, IReadOnlyList<OverlayRow> Rows, string? Estimation = null);

/// <summary>One figure of the overlay. <paramref name="Detail"/>: optional small grey text after the value.</summary>
public sealed record OverlayRow(string Label, string Value, bool Treasure = false, string? Detail = null);
