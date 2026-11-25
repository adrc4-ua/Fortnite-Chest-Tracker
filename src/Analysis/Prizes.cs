using System.Text.RegularExpressions;

namespace FCT.Analysis;

/// <summary>
/// Rewards of the treasure hunt ("Treasure Hunters", Chapter 7, 1 Nov – 5 Dec 2026): every chest has a small chance
/// to pay one out to the whole squad. Two families:
/// <list type="bullet">
/// <item>Featured treasures: the weekly cosmetics (outfit, pickaxe or back bling, jam track, loading screen, gold car)
/// and the Pirate Shop Access. The server sends their prize id ("DelilahPrize:dpid_item17", a GUID for the Pirate Shop
/// Access) with an animated chest, so they are named here.</item>
/// <item>Treasure gizmos: the five gizmos, in every week. The replay does not say which one was received.</item>
/// </list>
/// Names are the official English ones (also used by the Spanish game for these items). Weeks 4 and 5 come from the
/// files of the game as leaked (ShiinaBR, Fortnite.gg).
/// </summary>
public static class Prizes
{
    public enum Kind { Outfit, Pickaxe, BackBling, Vehicle, Decal, JamTrack, LoadingScreen, ShopAccess, Gizmo }

    /// <param name="Week">1–5, or 0 for every week.</param>
    /// <param name="Id">Prize id seen in replays (only for the ones already received).</param>
    public sealed record Prize(string Name, Kind Type, int Week, string? Id = null);

    /// <summary>
    /// Start of each week of the event (UTC). The prizes change on Sundays at 9 AM Eastern Time (14:00 UTC, 15:00 in
    /// Spain): a week 3 prize was won at 14:39 UTC on Sunday 15 November, in a replay. The event ends on <see cref="End"/>.
    /// </summary>
    public static readonly DateTime[] WeekStarts =
    {
        new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc), new(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc),
        new(2026, 11, 22, 14, 0, 0, DateTimeKind.Utc), new(2026, 11, 29, 14, 0, 0, DateTimeKind.Utc),
    };
    public static readonly DateTime End = new(2026, 12, 5, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The day the season of the hunt began (Treasure Hunters, update v43.00, 1 November 2026).</summary>
    public static readonly DateTime SeasonStart = new(2026, 11, 1);

    /// <summary>When a week (1–5) starts, in local time.</summary>
    public static DateTime StartOf(int week) => WeekStarts[week - 1].ToLocalTime();

    /// <summary>When a week (1–5) ends (the next one starts), in local time.</summary>
    public static DateTime EndOf(int week) => (week < WeekStarts.Length ? WeekStarts[week] : End).ToLocalTime();

    /// <summary>
    /// The prize id of the Pirate Shop Access: unlike every other prize (DelilahPrize:dpid_itemN), its row of the game's
    /// prize table (DT_DelilahTreasureHunt_Prizes, DelilahTreasure_6) carries a GUID.
    /// </summary>
    public const string PirateShopId = "9f73f94d-2e61-4523-97d7-d61235d3365e";

    /// <summary>
    /// Every prize of the hunt with its id, as in the game's prize table (DT_DelilahTreasureHunt_Prizes, build 43.10):
    /// each row has its own id, never reused from week to week. Week 1 is items 1 to 5, the gizmos 9 to 13 and weeks 2
    /// to 5 five items each from 14 (outfit, back bling, jam track, loading screen, gold car). Names are the official
    /// ones; weeks 4 and 5 come from the community.
    /// </summary>
    public static readonly Prize[] All =
    {
        // Every week
        new("Pirate Shop Access", Kind.ShopAccess, 0, PirateShopId),
        new("Extraction Accelerator", Kind.Gizmo, 0, "DelilahPrize:dpid_item9"),
        new("Portable Extractor", Kind.Gizmo, 0, "DelilahPrize:dpid_item10"),
        new("Golden Firework", Kind.Gizmo, 0, "DelilahPrize:dpid_item11"),
        new("Sprite Locator", Kind.Gizmo, 0, "DelilahPrize:dpid_item12"),
        new("Lucky Locator", Kind.Gizmo, 0, "DelilahPrize:dpid_item13"),
        // Week 1
        new("Solid Gold Rugahand", Kind.Outfit, 1, "DelilahPrize:dpid_item1"),
        new("Solid Gold Gauntlets", Kind.Pickaxe, 1, "DelilahPrize:dpid_item2"),
        new("No Trespassing", Kind.JamTrack, 1, "DelilahPrize:dpid_item3"),
        new("Plunder in Paradise", Kind.LoadingScreen, 1, "DelilahPrize:dpid_item4"),
        new("Mercedes-AMG G 63 'G-Wagon'", Kind.Vehicle, 1, "DelilahPrize:dpid_item5"),
        // Week 2
        new("Solid Gold Lil Uzi Vert", Kind.Outfit, 2, "DelilahPrize:dpid_item14"),
        new("Diamond and Gold", Kind.BackBling, 2, "DelilahPrize:dpid_item15"),
        new("London", Kind.JamTrack, 2, "DelilahPrize:dpid_item16"),
        new("So Kawaii", Kind.LoadingScreen, 2, "DelilahPrize:dpid_item17"),
        new("Cadillac CT5-V Blackwing", Kind.Vehicle, 2, "DelilahPrize:dpid_item18"),
        // Week 3: confirmed by the official cosmetics list (loadingscreen_delilahthw3, Backpack_FledGravelReward…) and by
        // prizes 20 to 22 won in real matches
        new("Solid Gold Miku", Kind.Outfit, 3, "DelilahPrize:dpid_item19"),
        new("Miku's Takarabako", Kind.BackBling, 3, "DelilahPrize:dpid_item20"),
        new("PPPP", Kind.JamTrack, 3, "DelilahPrize:dpid_item21"),
        new("May Fortune Find You", Kind.LoadingScreen, 3, "DelilahPrize:dpid_item22"),
        new("Chevrolet Corvette ZR1", Kind.Vehicle, 3, "DelilahPrize:dpid_item23"),
        // Weeks 4 and 5: the slots are in the table, the names as leaked from the game's files
        new("Solid Gold Kasane Teto", Kind.Outfit, 4, "DelilahPrize:dpid_item24"),
        new("Solid Gold High Tide and Wavecutter", Kind.BackBling, 4, "DelilahPrize:dpid_item25"),
        new("Wellerman", Kind.JamTrack, 4, "DelilahPrize:dpid_item26"),
        new("Oscar's Wraps", Kind.LoadingScreen, 4, "DelilahPrize:dpid_item27"),
        new("Lamborghini Urus SE", Kind.Vehicle, 4, "DelilahPrize:dpid_item28"),
        new("Solid Gold Captain X", Kind.Outfit, 5, "DelilahPrize:dpid_item29"),
        new("Roasted Meat", Kind.BackBling, 5, "DelilahPrize:dpid_item30"),
        new("I Follow Rivers", Kind.JamTrack, 5, "DelilahPrize:dpid_item31"),
        new("Jones and the Straw Hats", Kind.LoadingScreen, 5, "DelilahPrize:dpid_item32"),
        new("G-Wagon", Kind.Vehicle, 5, "DelilahPrize:dpid_item33"),
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

    /// <summary>
    /// Prize behind an id (each id is one prize, whatever the week). Any GUID is the Pirate Shop Access: the only
    /// prize of the table with a GUID for id (another year's token may come with its own).
    /// </summary>
    public static Prize? ById(string? id, int week = 0) =>
        id == null ? null
        : All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
          ?? (Guid.TryParse(id, out _) ? All.First(p => p.Type == Kind.ShopAccess) : null);
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
        var featured = f.NewAccountItem || (f.PrizeId != null && !TreasureNames.IsGizmoKey(f.PrizeId));
        if (featured)
        {
            // The player's own name in treasures.json wins; then the catalogue; else a generic name.
            if (f.PrizeId != null && TreasureNames.PrizeNames.TryGetValue(f.PrizeId, out var custom) && !string.IsNullOrWhiteSpace(custom))
            {
                var m = NameAndType.Match(custom);
                var name = m.Groups["name"].Value;
                var known = ByName(name);
                return (name.Length > 0 ? name : custom.Trim(), known != null ? TypeName(known.Type) : NullIfEmpty(m.Groups["type"].Value), true);
            }
            return ById(f.PrizeId, week) switch
            {
                { Type: Kind.Gizmo } gizmo => (Shown(gizmo), TypeName(Kind.Gizmo), false), // a gizmo's own prize id: still a gizmo
                { } p => (p.Name, TypeName(p.Type), true),
                null => (Featured, null, true),
            };
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

    /// <summary>Stars to display: ★★★ for Pirate Shop Access and weekly skins (outfits), ★★ for other featured treasures, ★ for gizmos.</summary>
    public static string Stars(TreasureFind find, string? name = null, string? type = null, bool? featured = null)
    {
        var isFeatured = featured ?? find.Featured;
        if (!isFeatured)
        {
            return "★";
        }
        var prize = ById(find.PrizeId) ?? ByName(name ?? Describe(find).Name);
        if (prize is { Type: Kind.ShopAccess or Kind.Outfit }
            || type == TypeName(Kind.ShopAccess)
            || type == TypeName(Kind.Outfit))
        {
            return "★★★";
        }
        return "★★";
    }

    /// <summary>Names and IDs of prizes found in matches.</summary>
    public static HashSet<string> DetectedPrizeNames(IEnumerable<(MatchRecord m, TreasureFind f)> treasures)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (match, find) in treasures)
        {
            if (find.PrizeId != null)
            {
                names.Add(find.PrizeId);
            }
            var local = (match.MatchStartUtc ?? match.AnalyzedAtUtc).ToLocalTime();
            names.Add(Describe(find, local).Name);
        }
        return names;
    }

    /// <summary>Whether a prize was detected in a match.</summary>
    public static bool IsDetected(Prize prize, HashSet<string> detected) =>
        detected.Contains(prize.Name) ||
        detected.Contains(Shown(prize)) ||
        (prize.Id != null && detected.Contains(prize.Id));

    /// <summary>How many weekly prizes were struck off by hand and not found in a match.</summary>
    public static int ManualMarksCount(IReadOnlyDictionary<string, bool> marks, HashSet<string> detected) =>
        All.Where(p => p.Week > 0).Count(p => !IsDetected(p, detected) && marks.TryGetValue(p.Name, out var m) && m);

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
