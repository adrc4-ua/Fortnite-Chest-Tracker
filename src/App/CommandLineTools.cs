namespace FCT.App;

/// <summary>The modes that run without the window: --once, --overlay-preview and --diagnose.</summary>
internal static class CommandLineTools
{
    /// <summary>--once: reads the finished replays, writes the stats page, prints the totals.</summary>
    public static int Once(Store store, Config config, TrackerData data, Tracker tracker)
    {
        tracker.Scan(ignoreQuietPeriod: true);
        // the game's own list of the player's prizes names the featured treasures the replays could not
        if (PrizeListLog.Update(data) | PrizeListMerge.Apply(data))
        {
            store.SaveData(data);
        }
        StatsPage.WriteAll(store, config, data);
        tracker.PrintSummary();
        Console.WriteLine(L.F("  Estadísticas: {0}", "  Stats: {0}", store.StatsPath));
        return 0;
    }

    /// <summary>
    /// --overlay-preview: draws the overlay off screen in its three states (live, in progress, waiting) and saves each as
    /// a PNG next to <paramref name="path"/>. The three latest matches count as "this session", in memory only.
    /// </summary>
    public static int OverlayPreview(Store store, Config config, string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }
        store.LoadPrizeNames();
        var shown = Profiles.View(store.LoadData(), config);
        foreach (var recent in shown.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).Take(3))
        {
            recent.MatchStartUtc = DateTime.UtcNow;
        }
        var initial = Profiles.InitialChests(config, shown.Matches.Select(Profiles.Key).FirstOrDefault());
        var live = new MatchRecord { ChestsOpened = 12, TreasureFinds = { new TreasureFind { Item = "Treasure" } } };

        NativeOverlay.Start(-20000, -20000, false, (_, _) => { }, _ => { }, onClose: () => { });
        // Live first: the overlay never shrinks, so the three pictures come out the same size, as in the game.
        var states = new (string Suffix, MatchRecord? Live, bool InProgress)[] { ("-en-vivo", live, true), ("-en-curso", null, true), ("-espera", null, false) };
        var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        foreach (var (suffix, figures, inProgress) in states)
        {
            NativeOverlay.Update(OverlayModel.Build(shown, figures, inProgress, initial));
            Thread.Sleep(600);
            var file = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + suffix + ".png");
            Console.WriteLine(NativeOverlay.Snapshot(file)
                ? L.F("Vista previa: {0}", "Preview: {0}", file)
                : L.F("Error al capturar: {0}", "Capture failed: {0}", file));
        }
        return 0;
    }

    /// <summary>
    /// --diagnose: reads a replay (or every replay of a folder) without saving anything and prints, match by match, the
    /// replay's independent signals and what does not add up.
    /// </summary>
    public static int Diagnose(Store store, string target)
    {
        store.LoadPrizeNames();
        var replays = Directory.Exists(target)
            ? new DirectoryInfo(target).GetFiles("*.replay").OrderBy(f => f.Name).ToList()
            : new List<FileInfo> { new(target) };
        foreach (var replay in replays)
        {
            Console.WriteLine();
            Console.WriteLine(replay.Name);
            if (!replay.Exists || ReplayHeaderProbe.KeyMissing(replay.FullName))
            {
                Console.WriteLine(L.T("  No se puede leer (no existe o es una partida en curso).", "  Cannot be read (missing, or a match in progress)."));
                continue;
            }
            try
            {
                var analyzer = new ReplayAnalyzer { Diagnosis = new List<string>() };
                using var content = new MemoryStream(File.ReadAllBytes(replay.FullName), writable: false);
                var match = analyzer.Analyze(content, replay.Name, "");
                Console.WriteLine($"  {L.DateTimeShort((match.MatchStartUtc ?? match.AnalyzedAtUtc).ToLocalTime())} · {Pretty.Playlist(match.Playlist)} · {match.PlayerName}");
                foreach (var line in analyzer.Diagnosis)
                {
                    Console.WriteLine("  " + line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  " + L.T("Error: ", "Error: ") + ex.Message);
            }
        }
        return 0;
    }
}
