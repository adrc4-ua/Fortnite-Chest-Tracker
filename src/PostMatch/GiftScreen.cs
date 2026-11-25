using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using FCT.Analysis;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace FCT.PostMatch;

/// <summary>
/// The "Treasure found!" screen the game shows back in the lobby for each new item a match gave the account (a
/// featured treasure, also one a teammate won far away): the item's type and its name in big plain text ("JAM TRACK –
/// 2021 | 2:29" / "WELLERMAN"). The game log says when it is shown (<see cref="GameLogWatcher"/>), but not which item.
/// </summary>
public sealed class GiftScreen
{
    /// <summary>When the game showed the screen (UTC, from the game log).</summary>
    public DateTime ShownUtc { get; set; }

    /// <summary>The prizes read on it, in the order they were shown.</summary>
    public List<string> PrizeIds { get; set; } = new();

    /// <summary>The text read (each capture's lines, joined), kept to check a reading later.</summary>
    public List<string> Text { get; set; } = new();
}

/// <summary>
/// Reads the "Treasure found!" screen with the text recognition built into Windows, in the game's language when
/// Windows has it (no images kept, nothing sent anywhere). The game can be in any language, so what does not depend on
/// it comes first: the prize whose official name is on screen (song titles and car names are the same in every
/// language); else the item type, from the jam tracks' "year | length" line (any language) or from the type's word in
/// the languages known here, which with the week gives the prize when that week has a single one of that type.
/// Anything else is left unread: the treasure still counts as a featured one.
/// </summary>
public static class GiftScreenReader
{
    /// <summary>A jam track's line under its type: "2021 | 2:29" (the bar is often read as 1, I or l).</summary>
    private static readonly System.Text.RegularExpressions.Regex JamTrackLine = new(@"\b(?:19|20)\d\d\s*[|Il1!]?\s*\d{1,2}[:.]\d\d\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Item types as the screen writes them (normalized: upper case, no accents), in the languages known for sure; the
    /// others still get the jam tracks (by their line) and every name that is not translated.
    /// </summary>
    private static readonly (Prizes.Kind Kind, string[] Words)[] Types =
    {
        (Prizes.Kind.JamTrack, new[] { "PISTA DE IMPROVISACION", "JAM TRACK" }),
        (Prizes.Kind.BackBling, new[] { "MOCHILA", "BACK BLING", "ACCESSOIRE DE DOS", "RUCKEN ACCESSOIRE", "DORSO DECORATIVO", "ACESSORIO PARA AS COSTAS" }),
        (Prizes.Kind.Outfit, new[] { "TRAJE", "OUTFIT", "TENUE", "COSTUME" }),
        (Prizes.Kind.LoadingScreen, new[] { "PANTALLA DE CARGA", "LOADING SCREEN", "ECRAN DE CHARGEMENT", "LADEBILDSCHIRM", "SCHERMATA DI CARICAMENTO", "TELA DE CARREGAMENTO" }),
        (Prizes.Kind.Pickaxe, new[] { "HERRAMIENTA DE RECOLECCION", "HARVESTING TOOL", "PICKAXE" }),
    };

    private static readonly object Gate = new();
    private static OcrEngine? _engine;
    private static bool _engineTried;

    /// <summary>Lines of text on the frame, or none when Windows has no text recognition for the user's languages.</summary>
    public static async Task<List<string>> ReadTextAsync(WindowCapture.Frame frame)
    {
        if (Engine() is not { } engine)
        {
            return new List<string>();
        }
        var scaled = Fit(frame, (int)OcrEngine.MaxImageDimension);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(scaled.Bgra.AsBuffer(), BitmapPixelFormat.Bgra8, scaled.Width, scaled.Height, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);
        return result.Lines.Select(l => l.Text).ToList();
    }

    private static OcrEngine? Engine()
    {
        lock (Gate)
        {
            if (!_engineTried)
            {
                _engineTried = true;
                _engine = GameLanguage() is { } game && OcrEngine.IsLanguageSupported(game)
                    ? OcrEngine.TryCreateFromLanguage(game)
                    : OcrEngine.TryCreateFromUserProfileLanguages();
            }
            return _engine;
        }
    }

    /// <summary>The game's language ("Culture=es" in its GameUserSettings.ini), or null when it cannot be told.</summary>
    private static Windows.Globalization.Language? GameLanguage()
    {
        try
        {
            var ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");
            var culture = File.Exists(ini)
                ? File.ReadLines(ini).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("Culture=", StringComparison.OrdinalIgnoreCase))?[8..].Trim()
                : null;
            return string.IsNullOrEmpty(culture) || !Windows.Globalization.Language.IsWellFormed(culture) ? null : new Windows.Globalization.Language(culture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The frame shrunk by a whole factor until it fits the recognizer (a 4K window, an ultrawide one).</summary>
    private static WindowCapture.Frame Fit(WindowCapture.Frame frame, int max)
    {
        var factor = 1;
        while (frame.Width / factor > max || frame.Height / factor > max)
        {
            factor++;
        }
        if (factor == 1)
        {
            return frame;
        }
        int width = frame.Width / factor, height = frame.Height / factor;
        var bgra = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                for (var c = 0; c < 4; c++)
                {
                    var sum = 0;
                    for (var dy = 0; dy < factor; dy++)
                    {
                        for (var dx = 0; dx < factor; dx++)
                        {
                            sum += frame.Bgra[((y * factor + dy) * frame.Width + x * factor + dx) * 4 + c];
                        }
                    }
                    bgra[(y * width + x) * 4 + c] = (byte)(sum / (factor * factor));
                }
            }
        }
        return new WindowCapture.Frame(width, height, bgra);
    }

    /// <summary>
    /// The prize the screen's text shows, or null. A prize's official name anywhere in it (a long one may be split over
    /// two lines) names it; among names inside others ("G-Wagon"), the prize of that week, then the longest name.
    /// Without a name, the item type gives the prize when that week has a single one of that type.
    /// </summary>
    public static string? PrizeOf(IReadOnlyList<string> lines, DateTime whenUtc)
    {
        var text = Normalize(string.Join(" ", lines));
        var compact = text.Replace(" ", "", StringComparison.Ordinal);
        var week = Prizes.WeekOf(whenUtc);
        var featured = Prizes.All.Where(p => p.Id != null && p.Type != Prizes.Kind.Gizmo && (week == 0 || p.Week <= week)).ToList();
        var named = featured
            .Where(p => Normalize(p.Name).Replace(" ", "", StringComparison.Ordinal) is { Length: >= 4 } name && compact.Contains(name, StringComparison.Ordinal))
            .OrderByDescending(p => p.Week == week)
            .ThenByDescending(p => p.Name.Length)
            .FirstOrDefault();
        if (named != null)
        {
            return named.Id;
        }
        if (week == 0)
        {
            return null;
        }
        var kinds = Types.Where(t => t.Words.Any(w => Contains(text, w))).Select(t => t.Kind).ToList();
        if (lines.Any(l => JamTrackLine.IsMatch(l)))
        {
            kinds.Insert(0, Prizes.Kind.JamTrack);
        }
        foreach (var kind in kinds.Distinct())
        {
            if (featured.Where(p => p.Week == week && p.Type == kind).ToList() is { Count: 1 } only)
            {
                return only[0].Id;
            }
        }
        return null;
    }

    /// <summary>A whole word or phrase of the normalized text.</summary>
    private static bool Contains(string text, string phrase) => $" {text} ".Contains($" {phrase} ", StringComparison.Ordinal);

    /// <summary>Upper case, without accents, only letters and digits, single spaces.</summary>
    internal static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            builder.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
