using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FCT.PostMatch;

/// <summary>
/// Follows the Fortnite client log (read only, like the replays) and tells when the game shows the post-match
/// "Treasure found" screen, and the lobby's screen of new items (<see cref="GiftScreen"/>). Only new lines are read:
/// nothing that was already in the file when FCT started.
/// </summary>
public sealed class GameLogWatcher : IDisposable
{
    // FCT_GAME_LOG: another log file, to test without Fortnite.
    public static string DefaultPath => Environment.GetEnvironmentVariable("FCT_GAME_LOG") is { Length: > 0 } test ? test : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame", "Saved", "Logs", "FortniteGame.log");

    // [2026.11.12-02.49.17:689][...]LogDynamicUI: Adding scene: UIScene_DelilahTreasureHunt_TreasuresFound_PostMatch [...]
    private static readonly Regex ScreenLine = new(
        @"^\[(\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}):\d{3}\].*Adding scene: UIScene_\w*TreasuresFound_PostMatch", RegexOptions.Compiled);

    // [2026.11.25-01.03.42:366][877]LogGiftBox: GiftBoxScreen_C_2147482646 shown
    private static readonly Regex GiftLine = new(
        @"^\[(\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}):\d{3}\].*LogGiftBox: GiftBoxScreen\w* shown", RegexOptions.Compiled);

    private readonly string _path;
    private readonly Action<DateTime> _onTreasureScreen;
    private readonly Action<DateTime>? _onGiftScreen;
    private readonly System.Threading.Timer _timer;
    private long _position = -1;
    private string _partial = "";
    private int _busy;

    /// <param name="onTreasureScreen">Called (on a pool thread) with the UTC time the screen was shown.</param>
    /// <param name="onGiftScreen">Same for the lobby's screen of new items.</param>
    public GameLogWatcher(Action<DateTime> onTreasureScreen, string? path = null, Action<DateTime>? onGiftScreen = null)
    {
        _path = path ?? DefaultPath;
        _onTreasureScreen = onTreasureScreen;
        _onGiftScreen = onGiftScreen;
        _timer = new System.Threading.Timer(_ => Poll(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }
        try
        {
            var info = new FileInfo(_path);
            if (!info.Exists)
            {
                _position = 0;
                return;
            }
            if (_position < 0)
            {
                _position = info.Length; // start at the end: only what happens from now on
                return;
            }
            if (info.Length < _position)
            {
                _position = 0; // the game started a new log
                _partial = "";
            }
            if (info.Length == _position)
            {
                return;
            }
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(_position, SeekOrigin.Begin);
            var buffer = new byte[Math.Min(info.Length - _position, 4 * 1024 * 1024)];
            var read = stream.Read(buffer, 0, buffer.Length);
            _position += read;
            var text = _partial + Encoding.UTF8.GetString(buffer, 0, read);
            var lines = text.Split('\n');
            _partial = lines[^1];
            foreach (var line in lines.Take(lines.Length - 1))
            {
                if (line.Contains("TreasuresFound_PostMatch", StringComparison.Ordinal) && ScreenLine.Match(line) is { Success: true } m
                    && DateTime.TryParseExact(m.Groups[1].Value, "yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
                {
                    _onTreasureScreen(utc);
                }
                else if (_onGiftScreen != null && line.Contains("LogGiftBox", StringComparison.Ordinal) && GiftLine.Match(line) is { Success: true } g
                    && DateTime.TryParseExact(g.Groups[1].Value, "yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var giftUtc))
                {
                    _onGiftScreen(giftUtc);
                }
            }
        }
        catch (IOException)
        {
            // the game is rotating its log: next time
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Game log", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public void Dispose() => _timer.Dispose();
}
