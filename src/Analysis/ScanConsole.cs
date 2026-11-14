using System.Text;

namespace FCT.Analysis;

/// <summary>What a replay reading ended in.</summary>
internal sealed record ScanResult(MatchRecord? Match = null, bool Updated = false, string? Skipped = null, string? Error = null, List<string>? NewPrizeIds = null);

/// <summary>
/// The console side of scanning (the log of the window's process and the --once output): one line per replay at the
/// end of a match, a summary for a batch of earlier replays, and the profile totals.
/// </summary>
internal sealed class ScanConsole
{
    private readonly Store _store;
    private readonly Config _config;
    private readonly TrackerData _data;

    public ScanConsole(Store store, Config config, TrackerData data)
    {
        _store = store;
        _config = config;
        _data = data;
    }

    /// <summary>"[hh:mm:ss] Partida terminada: analizando…" (just ended) or "[hh:mm:ss] Analizando file…" (older).</summary>
    public void Reading(FileInfo file)
    {
        var size = (file.Length / 1024 / 1024.0).ToString("F1", L.Culture);
        var endedNow = DateTime.UtcNow - file.LastWriteTimeUtc < TimeSpan.FromMinutes(2);
        Line(ConsoleColor.DarkGray, endedNow
            ? L.F("[{0:HH:mm:ss}] Partida terminada: analizando el replay ({1} MB)…", "[{0:HH:mm:ss}] Match over: analyzing the replay ({1} MB)…", DateTime.Now, size)
            : L.F("[{0:HH:mm:ss}] Analizando {1} ({2} MB)…", "[{0:HH:mm:ss}] Analyzing {1} ({2} MB)…", DateTime.Now, file.Name, size));
    }

    public void One(ScanResult result)
    {
        if (result.Error != null)
        {
            Line(ConsoleColor.Red, L.F("   No se puede leer este replay: {0}", "   Cannot read this replay: {0}", result.Error));
            ErrorLogHint();
            return;
        }
        if (result.Skipped != null)
        {
            Console.WriteLine($"   {L.T("Ignorado", "Skipped")}: {result.Skipped}.");
            return;
        }
        var match = result.Match!;
        Match(match, result.Updated);
        var shown = Profiles.Current(_data, _config);
        if (shown != null && Profiles.Key(match) != shown.Key)
        {
            Line(ConsoleColor.DarkYellow, L.F("   Partida jugada en otra cuenta ({0}): cuenta para su propio perfil. Perfil mostrado: {1}.",
                "   Match played on another account ({0}): it counts for its own profile. Profile shown: {1}.",
                match.PlayerName ?? L.T("cuenta desconocida", "unknown account"), shown.Name));
        }
        PrizesToName(result.NewPrizeIds);
    }

    public void Batch(IReadOnlyList<ScanResult> results)
    {
        var matches = results.Where(r => r.Match != null).Select(r => r.Match!).ToList();
        var errors = results.Where(r => r.Error != null).Select(r => r.Error!).ToList();
        var skipped = results.Count(r => r.Skipped != null);
        var treasures = matches.Sum(m => m.TreasureFinds.Count);

        Part(ConsoleColor.Green, "  ✔ " + L.Count(results.Count, "replay analizado", "replays analizados", "replay analyzed", "replays analyzed"));
        Console.Write(": ");
        Part(ConsoleColor.Yellow, L.Count(matches.Count, "partida", "partidas", "match", "matches") + ", " + L.Count(matches.Sum(m => m.ChestsOpened), "cofre", "cofres", "chest", "chests"));
        if (treasures > 0)
        {
            Part(ConsoleColor.Cyan, ", ★ " + L.Count(treasures, "tesoro", "tesoros", "treasure", "treasures"));
        }
        Console.WriteLine();
        if (skipped > 0)
        {
            Line(ConsoleColor.DarkGray, L.F("    {0} ignorado(s): versiones anteriores, modos creativos o partidas ya contadas.",
                "    {0} skipped: older versions, Creative modes or matches already counted.", skipped));
        }
        if (errors.Count > 0)
        {
            var list = new StringBuilder(L.F("    {0} replay(s) ilegible(s):", "    {0} unreadable replay(s):", errors.Count));
            foreach (var error in errors.Take(5))
            {
                list.Append(Environment.NewLine).Append("      ").Append(error);
            }
            Line(ConsoleColor.Red, list.ToString());
            ErrorLogHint();
        }
        PrizesToName(results.Where(r => r.NewPrizeIds != null).SelectMany(r => r.NewPrizeIds!).ToList());
    }

    /// <summary>"Nombre | Partidas | Cofres | Tesoros | Media" of the profile on screen.</summary>
    public void Totals()
    {
        var profile = Profiles.Current(_data, _config);
        var view = Profiles.View(_data, _config);
        var matches = view.Matches;
        var counted = matches.Sum(m => m.ChestsOpened);
        var text = new StringBuilder();
        if (profile != null)
        {
            text.Append(profile.Name).Append("   |   ");
        }
        text.Append(L.F("Partidas: {0}   |   Cofres: {1}   |   Tesoros: {2}", "Matches: {0}   |   Chests: {1}   |   Treasures: {2}",
            L.N(Profiles.AllMatches(_data, _config, view)), L.N(counted + Profiles.InitialChests(_data, _config)), L.N(matches.Sum(m => m.TreasureFinds.Count))));
        if (matches.Count > 0)
        {
            text.Append(L.F("   |   Media: {0} / partida", "   |   Average: {0} / match", ((double)counted / matches.Count).ToString("F1", L.Culture)));
        }
        Line(ConsoleColor.Cyan, "  " + text);
    }

    /// <summary>One match: when, mode, place, chests (broken ones noted), the profile total, then each treasure.</summary>
    private void Match(MatchRecord stored, bool updated)
    {
        var match = Profiles.Adjust(stored, _config)!;
        var key = Profiles.Key(match);
        var total = Profiles.InitialChests(_config, key)
                    + _data.Matches.Where(m => Profiles.Key(m) == key && Profiles.Counted(m, _config)).Sum(m => Profiles.Adjust(m, _config)!.ChestsOpened);
        var broken = match.Chests.Count(c => c.Destroyed);

        Part(ConsoleColor.Green, $"   {(updated ? "↻" : "✔")} {L.T("Partida del", "Match of")} {L.DateTimeShort((match.MatchStartUtc ?? match.AnalyzedAtUtc).ToLocalTime())}");
        Console.Write($" ({Pretty.Playlist(match.Playlist)}, {Pretty.Placement(match)}): ");
        Part(ConsoleColor.Yellow, L.Count(match.ChestsOpened, "cofre", "cofres", "chest", "chests") + (broken > 0 ? L.F(" ({0} destruido(s))", " ({0} destroyed)", broken) : ""));
        Console.WriteLine($"  →  total: {L.N(total)}");

        foreach (var find in match.TreasureFinds)
        {
            Line(find.FromPrizeChest ? ConsoleColor.Yellow : ConsoleColor.Cyan,
                $"   {(find.FromPrizeChest ? "★★" : "★")} {L.T("Tesoro obtenido: ", "Treasure obtained: ")}{find.DisplayName} ({Pretty.Obtained(find)})");
            if (!find.SourceIsChest)
            {
                Line(ConsoleColor.Cyan, L.F("   ⚑ Origen inusual: {0}{1} [{2}] → anotado en las estadísticas", "   ⚑ Unusual source: {0}{1} [{2}] → noted in stats",
                    find.SourceLabel, find.SourceCertain ? "" : L.T(" (probable)", " (likely)"), find.SourceBlueprint));
            }
        }
    }

    /// <summary>Real prize ids nobody has named yet (gizmo keys are not worth asking about).</summary>
    private void PrizesToName(List<string>? ids)
    {
        var real = ids?.Where(id => !TreasureNames.IsGizmoKey(id)).ToList();
        if (real is { Count: > 0 })
        {
            Line(ConsoleColor.DarkYellow, L.F("   Tesoro(s) por nombrar en {0}: {1}", "   Treasure(s) to name in {0}: {1}", _store.PrizeNamesPath, string.Join(", ", real)));
        }
    }

    private void ErrorLogHint() =>
        Line(ConsoleColor.DarkGray, L.F("   Detalles en {0}. Suele deberse a una actualización de Fortnite: notifícalo en {1}",
            "   Details in {0}. Usually caused by a Fortnite update: report it at {1}", _store.ErrorLogPath, AppInfo.IssuesUrl));

    private static void Line(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static void Part(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }
}
