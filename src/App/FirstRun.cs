using System.Diagnostics;
using System.Windows.Forms;

namespace FCT.App;

/// <summary>
/// The very first start: a newer version is offered first; then the welcome (turn replays on in Fortnite, and whether to
/// count the matches already saved) and the earlier-matches estimate. Nothing is saved until every step is done:
/// closing any of these windows leaves FCT as if it had never been opened.
/// </summary>
internal static class FirstRun
{
    public enum Outcome { Continue, Closed, Updated }

    public static Outcome Run(Config config, Tracker tracker, SingleInstance instance, string dataDir, bool defaultFolder)
    {
        if (OfferNewerVersion(config, instance) is { } stop)
        {
            return stop;
        }

        var saved = tracker.UnprocessedFiles().Where(ReplayFolder.NotRecording).ToList();
        var countSaved = Welcome(saved.Count);
        if (countSaved == null)
        {
            return Outcome.Closed;
        }
        // Without saved matches to count (none, or "only new ones"), the estimate's first choice is "start from zero".
        if (EstimateDialog.Ask(null, null, null, null, null, fromScratch: saved.Count == 0 || countSaved == false) is not { } earlier)
        {
            return Outcome.Closed;
        }

        if (saved.Count > 0 && countSaved == false)
        {
            tracker.MarkSkipped(saved);
        }
        if (earlier.Estimate)
        {
            // No profile yet: kept under "" until the first counted match gives it one.
            config.Estimates[""] = new HistoryEstimate
            {
                TotalMatches = earlier.TotalMatches,
                SnapshotUtc = DateTime.UtcNow,
                Source = earlier.Source,
                SampleSize = earlier.Sample,
                ManualAverage = earlier.ManualAverage,
            };
        }
        // "Open with Fortnite" starts on (Settings turns it off). Only the normal data folder sets up Windows' side; a
        // test folder (--data) just keeps the choice.
        if (defaultFolder)
        {
            config.StartWithFortnite = OperatingSystem.IsWindows() && AppInfo.ExePath != null && FortniteWatch.Apply(true, dataDir);
        }
        return Outcome.Continue;
    }

    /// <summary>
    /// An older copy offers the latest one before anything else. Installed: the new exe starts (and welcomes again).
    /// Declined: updates switch to "ask". Null to go on with this copy.
    /// </summary>
    private static Outcome? OfferNewerVersion(Config config, SingleInstance instance)
    {
        if (config.UpdateMode == "off" || AppInfo.ExePath == null || Updater.CheckAsync().GetAwaiter().GetResult() is not { } newer)
        {
            return null;
        }
        var answer = InfoDialog.Ask(null, L.F("Hay una versión nueva de {0}", "There's a new version of {0}", AppInfo.ShortName),
            L.T("Tarda unos segundos. ¿La descargamos antes de empezar?", "It only takes a few seconds. Shall we get it before you start?"),
            Array.Empty<InfoPoint>(), L.T("Descargar", "Download"), L.T("Ahora no", "Not now"), Icons.Download);
        switch (answer)
        {
            case null:
                return Outcome.Closed;
            case false:
                config.UpdateMode = "prompt";
                return null;
        }
        if (Updater.Install(newer) is { } problem)
        {
            MessageBox.Show(L.F("No se pudo actualizar: {0}", "Update failed: {0}", problem), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        if (AppInfo.ExePath is not { } updated)
        {
            return null;
        }
        instance.Release();
        // the same options again (a test copy keeps its own data folder), plus the new version's announcement
        var again = Environment.GetCommandLineArgs().Skip(1).Where(a => a is not ("--after-update" or "--show"))
            .Select(a => a.Contains(' ') ? $"\"{a}\"" : a).Append("--after-update").Append("--show");
        Process.Start(new ProcessStartInfo(updated, string.Join(" ", again)) { UseShellExecute = true })?.Dispose();
        return Outcome.Updated;
    }

    /// <summary>What FCT does, the one thing to turn on in Fortnite, and the saved matches. Null when closed.</summary>
    private static bool? Welcome(int saved)
    {
        var replaysPoint = new InfoPoint(Icons.RecordReplays, Theme.GoldHi, L.T("Activa las repeticiones en Fortnite", "Turn on replays in Fortnite"),
            L.T("Ajustes → Juego → Repeticiones → «Grabar repeticiones». Sin esto no hay nada que contar.",
                "Settings → Game → Replays → \"Record Replays\". Without it there's nothing to count."));
        var secondPoint = saved > 0
            ? new InfoPoint(Icons.Folder, Theme.GoldHi,
                L.F("Tienes {0} guardadas", "You have {0} saved", L.Count(saved, "partida", "partidas", "match", "matches")),
                L.T("¿Las contamos ya? Si no, empezamos con las próximas.", "Shall we count them now? If not, we start with your next ones."))
            : new InfoPoint(Icons.Matches, Theme.GoldHi, L.T("Y a jugar", "Then just play"),
                L.T("Al acabar cada partida, tus cofres se suman solos.", "After each match, your chests are added up on their own."));
        return InfoDialog.Ask(null, L.F("Te damos la bienvenida a {0}", "Welcome to {0}", AppInfo.ShortName),
            L.T("Cuenta los cofres que abres en Fortnite, sin que tengas que hacer nada.", "It counts the chests you open in Fortnite, without you doing a thing."),
            new[] { replaysPoint, secondPoint },
            saved > 0 ? L.T("Contarlas", "Count them") : L.T("Empezar", "Start"),
            saved > 0 ? L.T("Solo las nuevas", "Only new ones") : null,
            Icons.Sparkle);
    }
}
