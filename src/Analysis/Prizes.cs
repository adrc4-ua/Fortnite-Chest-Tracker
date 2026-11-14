using System.Text.RegularExpressions;

namespace FCT.Analysis;

/// <summary>
/// Rewards of the treasure hunt ("Treasure Hunters", Chapter 7, 1 Nov – 5 Dec 2026): every chest has a small chance
/// to pay one out to the whole squad. Two families:
/// <list type="bullet">
/// <item>Featured treasures: the weekly cosmetics (outfit, pickaxe, vehicle…) and the Pirate Shop Access. The server
/// sends their prize id ("DelilahPrize:dpid_item3") with an animated chest, so they are named here.</item>
/// <item>Treasure gizmos: the five gizmos, in every week. The replay does not say which one was received.</item>
/// </list>
/// Names are the official English ones (also used by the Spanish game for these items). Weeks 3 to 5 come from the
/// files of the game as leaked (ShiinaBR, Fortnite.gg), marked unconfirmed until they are in the game.
/// </summary>
public static class Prizes
{
    public enum Kind { Outfit, Pickaxe, BackBling, Vehicle, Decal, JamTrack, LoadingScreen, ShopAccess, Gizmo }

    /// <param name="Week">1–5, or 0 for every week.</param>
    /// <param name="Id">Prize id seen in replays (only for the ones already received).</param>
    /// <param name="Announced">False when only reported by the community, not yet in the game.</param>
    public sealed record Prize(string Name, Kind Type, int Week, string? Id = null, bool Announced = true);

    /// <summary>
    /// Start of each week of the event (UTC). The prizes change on Sundays at 9 PM Eastern Time (Monday 2:00 UTC, 3:00 in
    /// Spain); the event ends on <see cref="End"/>.
    /// </summary>
    public static readonly DateTime[] WeekStarts =
    {
        new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new(2026, 11, 9, 2, 0, 0, DateTimeKind.Utc), new(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc),
        new(2026, 11, 23, 2, 0, 0, DateTimeKind.Utc), new(2026, 11, 30, 2, 0, 0, DateTimeKind.Utc),
    };
    public static readonly DateTime End = new(2026, 12, 5, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The day the season of the hunt began (Treasure Hunters, update v43.00, 1 November 2026).</summary>
    public static readonly DateTime SeasonStart = new(2026, 11, 1);

    /// <summary>When a week (1–5) starts, in local time.</summary>
    public static DateTime StartOf(int week) => WeekStarts[week - 1].ToLocalTime();

    /// <summary>When a week (1–5) ends (the next one starts), in local time.</summary>
    public static DateTime EndOf(int week) => (week < WeekStarts.Length ? WeekStarts[week] : End).ToLocalTime();

    public static readonly Prize[] All =
    {
        // Every week
        new("Pirate Shop Access", Kind.ShopAccess, 0),
        new("Golden Firework", Kind.Gizmo, 0),
        new("Portable Extractor", Kind.Gizmo, 0),
        new("Sprite Locator", Kind.Gizmo, 0),
        new("Lucky Locator", Kind.Gizmo, 0),
        new("Extraction Accelerator", Kind.Gizmo, 0),
        // Week 1
        new("Solid Gold A$AP Rocky", Kind.Outfit, 1),
        new("Solid Gold A$AP Rocky Gauntlets", Kind.Pickaxe, 1, "DelilahPrize:dpid_item1"),
        new("Plunder in Paradise", Kind.LoadingScreen, 1, "DelilahPrize:dpid_item2"),
        new("Mercedes-AMG G 63 'G-Wagon'", Kind.Vehicle, 1, "DelilahPrize:dpid_item3"),
        new("No Trespassing", Kind.JamTrack, 1, "DelilahPrize:dpid_item4"),
        new("Golden Llama Crew", Kind.Decal, 1),
        // Week 2. The prize ids are slots, the same every week (item1 pickaxe / back bling, item2 loading screen,
        // item3 vehicle, item4 jam track): the item behind each one changes with the week.
        new("Solid Gold Lil Uzi Vert", Kind.Outfit, 2),
        new("Diamond and Gold", Kind.BackBling, 2, "DelilahPrize:dpid_item1"),
        new("So Kawaii", Kind.LoadingScreen, 2, "DelilahPrize:dpid_item2"),
        new("Cadillac CT5-V Blackwing", Kind.Vehicle, 2, "DelilahPrize:dpid_item3"),
        new("London", Kind.JamTrack, 2, "DelilahPrize:dpid_item4"),
        // Weeks 3 to 5, as leaked from the game's files (same slots as week 2)
        new("Solid Gold Miku", Kind.Outfit, 3, Announced: false),
        new("Miku's Takarabako", Kind.BackBling, 3, "DelilahPrize:dpid_item1", Announced: false),
        new("May Fortune Find You", Kind.LoadingScreen, 3, "DelilahPrize:dpid_item2", Announced: false),
        new("Chevrolet Corvette ZR1", Kind.Vehicle, 3, "DelilahPrize:dpid_item3", Announced: false),
        new("PPPP", Kind.JamTrack, 3, "DelilahPrize:dpid_item4", Announced: false),
        new("Solid Gold Kasane Teto", Kind.Outfit, 4, Announced: false),
        new("Rogue Wave and Tidewalker", Kind.BackBling, 4, "DelilahPrize:dpid_item1", Announced: false),
        new("Oscar's Wraps", Kind.LoadingScreen, 4, "DelilahPrize:dpid_item2", Announced: false),
        new("Lamborghini Urus SE", Kind.Vehicle, 4, "DelilahPrize:dpid_item3", Announced: false),
        new("Sea Shanty", Kind.JamTrack, 4, "DelilahPrize:dpid_item4", Announced: false),
        new("Solid Gold Captain X", Kind.Outfit, 5, Announced: false),
        new("Roasted Meat", Kind.BackBling, 5, "DelilahPrize:dpid_item1", Announced: false),
        new("Jones and the Straw Hats", Kind.LoadingScreen, 5, "DelilahPrize:dpid_item2", Announced: false),
        new("G-Wagon", Kind.Vehicle, 5, "DelilahPrize:dpid_item3", Announced: false),
        new("I Follow Rivers", Kind.JamTrack, 5, "DelilahPrize:dpid_item4", Announced: false),
    };

    /// <summary>Week of the event (1–5) at a moment (local or UTC time), or 0 outside the event.</summary>
    public static int WeekOf(DateTime when)
    {
        var utc = when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime();
        if (utc < WeekStarts[0] || utc >= End)
        {
            return 0;
        }
        var week = 0;
        for (var i = 0; i < WeekStarts.Length; i++)
        {
            if (utc >= WeekStarts[i]) week = i + 1;
        }
        return week;
    }

    /// <summary>Prize behind an id in a week of the event (0: the week is not known, the first one that matches).</summary>
    public static Prize? ById(string? id, int week = 0) =>
        id == null ? null : All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase) && (week == 0 || p.Week == 0 || p.Week == week));
    /// <summary>Prize by its name: the stored (English) one or the one shown in the current language.</summary>
    public static Prize? ByName(string? name) =>
        name == null ? null : All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                                                      || string.Equals(Shown(p), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Name shown for a prize: gizmos in the current language (their name in the game); the weekly cosmetics keep their
    /// official name, which the Spanish game also uses.
    /// </summary>
    public static string Shown(Prize p) => p.Type == Kind.Gizmo ? L.Name(p.Name) : p.Name;

    public static string TypeName(Kind kind) => kind switch
    {
        Kind.Outfit => L.T("Traje", "Outfit"),
        Kind.Pickaxe => L.T("Pico", "Pickaxe"),
        Kind.BackBling => L.T("Mochila", "Back Bling"),
        Kind.Vehicle => L.T("Vehículo", "Vehicle"),
        Kind.Decal => L.T("Adhesivo de vehículo", "Vehicle decal"),
        Kind.JamTrack => L.T("Pista de improvisación", "Jam Track"),
        Kind.LoadingScreen => L.T("Pantalla de carga", "Loading Screen"),
        Kind.ShopAccess => L.T("Acceso a la tienda", "Item Shop access"),
        _ => L.T("Artilugio", "Gizmo"),
    };

    public static string Featured => L.T("Tesoro destacado", "Featured treasure");

    // A name from treasures.json may carry its type in brackets: "Name (type)".
    private static readonly Regex NameAndType = new(@"^\s*(?<name>.*?)\s*(\((?<type>[^()]*)\))?\s*\??\s*$", RegexOptions.Compiled);

    /// <summary>Display of one treasure: clean name, type (or null) and whether it is a featured treasure.</summary>
    public static (string Name, string? Type, bool Featured) Describe(TreasureFind f, DateTime? matchLocal = null)
    {
        var week = matchLocal is DateTime d ? WeekOf(d) : 0;
        var featured = f.PrizeId != null && !TreasureNames.IsGizmoKey(f.PrizeId);
        if (featured)
        {
            // The player's own name in treasures.json wins; then the catalogue; else a generic name.
            if (TreasureNames.PrizeNames.TryGetValue(f.PrizeId!, out var custom) && !string.IsNullOrWhiteSpace(custom))
            {
                var m = NameAndType.Match(custom);
                var name = m.Groups["name"].Value;
                var known = ByName(name);
                return (name.Length > 0 ? name : custom.Trim(), known != null ? TypeName(known.Type) : NullIfEmpty(m.Groups["type"].Value), true);
            }
            return ById(f.PrizeId, week) is { } p ? (p.Name, TypeName(p.Type), true) : (Featured, null, true);
        }

        // Gizmo read on the post-match screen.
        if (f.ScreenName is { } screenName && ByName(screenName) is { Type: Kind.Gizmo } read)
        {
            return (Shown(read), TypeName(Kind.Gizmo), false);
        }

        // Gizmo: named when the item is known, else the generic name.
        var custom2 = f.PrizeId != null && TreasureNames.PrizeNames.TryGetValue(f.PrizeId, out var n2) && !string.IsNullOrWhiteSpace(n2) ? n2.Trim() : null;
        if (custom2 != null)
        {
            return (custom2, TypeName(Kind.Gizmo), false);
        }
        var item = TreasureNames.GizmoName(f.Item);
        return ByName(item) is { Type: Kind.Gizmo } g ? (Shown(g), TypeName(Kind.Gizmo), false) : (L.Name(TreasureNames.UnknownGizmo), null, false);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
