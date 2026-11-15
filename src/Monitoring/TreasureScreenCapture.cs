using System.Collections.Concurrent;
using System.Diagnostics;

namespace FCT.Monitoring;

/// <summary>
/// The end-of-match treasure screen. When the game log says it is shown, only the game's window is captured (even
/// behind others) several times while its cards appear and settle, and the readings are combined card by card. Images
/// only live in memory while being read; a minimized or uncapturable window simply leaves the match as it was.
/// </summary>
internal sealed class TreasureScreenCapture
{
    /// <summary>
    /// The screen often stays only 2 to 7 s (the player leaves at once), sometimes much longer: captures every half
    /// second over the first 6 s, then every 1.5 s up to 12 s. The first one waits out the cards' entry animation
    /// (each card flashes gold as it lands, and reads wrong until it settles).
    /// </summary>
    private static readonly int[] CaptureDelaysMs = { 800, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 1500, 1500, 1500, 1500 };

    private readonly ConcurrentQueue<TreasureScreen> _read = new();
    private readonly Action _wake;

    public TreasureScreenCapture(Action wake) => _wake = wake;

    public void Shown(DateTime shownUtc) => _ = Task.Run(async () =>
    {
        try
        {
            var readings = new List<List<string>>();
            foreach (var delay in CaptureDelaysMs)
            {
                await Task.Delay(delay);
                var frame = await WindowCapture.CaptureAsync(GameWindow(), TimeSpan.FromSeconds(2));
                if (frame != null)
                {
                    readings.Add(TreasureScreenReader.Read(frame).Select(c => c.Item).ToList());
                }
            }
            if (Combine(readings) is { Count: > 0 } cards)
            {
                _read.Enqueue(new TreasureScreen { ShownUtc = shownUtc, Cards = cards, Readings = readings });
                _wake();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Pantalla de tesoros", ex);
        }
    });

    /// <summary>
    /// One reading out of several. A capture often misses a card (caught while it was still landing, or in a frame
    /// where it did not stand out), but a phantom card never shows up in more than one: so there are as many cards as
    /// at least two captures saw (a single capture only counts when it is the only one). Each card's name is voted
    /// among the captures that saw it; the cards settle and never flash again, so the vote runs over the later half
    /// of them (at least two), and among equals the name read later wins. A card read as a gizmo only once among many
    /// unknown readings (a featured treasure) stays unknown.
    /// </summary>
    internal static List<string> Combine(IReadOnlyList<List<string>> readings)
    {
        var counted = readings.Where(r => r.Count > 0).ToList();
        if (counted.Count == 0)
        {
            return new List<string>();
        }
        var needed = Math.Min(2, counted.Count);
        var count = counted.Max(r => r.Count);
        while (count > 0 && counted.Count(r => r.Count >= count) < needed)
        {
            count--;
        }
        var cards = new List<string>(count);
        for (var slot = 0; slot < count; slot++)
        {
            var rows = counted.Where(r => r.Count > slot).ToList();
            var window = rows.Skip(rows.Count - Math.Max(2, (rows.Count + 1) / 2)).ToList();
            var seen = window.Select(r => r[slot]).ToList();
            var unknown = seen.Count(name => name == TreasureScreenReader.Unknown);
            var named = seen.Where(name => name != TreasureScreenReader.Unknown)
                .GroupBy(name => name)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => seen.LastIndexOf(g.Key))
                .FirstOrDefault();
            // a featured treasure reads as unknown: one stray gizmo reading among them does not rename it
            var trusted = named != null && (named.Count() >= 2 || named.Count() >= unknown);
            cards.Add(trusted ? named!.Key : TreasureScreenReader.Unknown);
        }
        return cards;
    }

    /// <summary>The game's window; FCT_TREASURE_WINDOW (an exact window title) stands in for it in tests.</summary>
    private static IntPtr GameWindow()
    {
        if (Environment.GetEnvironmentVariable("FCT_TREASURE_WINDOW") is { Length: > 0 } title)
        {
            return PostMatchTools.FindWindowByTitle(title);
        }
        foreach (var process in Process.GetProcessesByName("FortniteClient-Win64-Shipping"))
        {
            using (process)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Files the screens read since last time (one per screen: two within 30 s are the same) and gives every match its
    /// screen again. True when the history changed (the caller saves it).
    /// </summary>
    public bool FileInto(TrackerData data)
    {
        var added = false;
        while (_read.TryDequeue(out var screen))
        {
            if (data.TreasureScreens.TrueForAll(s => Math.Abs((s.ShownUtc - screen.ShownUtc).TotalSeconds) > 30))
            {
                data.TreasureScreens.Add(screen);
                added = true;
            }
        }
        if (added)
        {
            foreach (var match in data.Matches)
            {
                TreasureScreenMerge.Apply(match, data.TreasureScreens);
            }
        }
        return added;
    }
}
