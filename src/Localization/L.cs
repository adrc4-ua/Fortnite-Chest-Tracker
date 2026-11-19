using System.Globalization;

namespace FCT.Localization;

/// <summary>
/// The app's texts. Spanish and English live in the code, side by side: <c>L.T("español", "English")</c>. Any other
/// language is a file (<see cref="LanguagePacks"/>); whatever it does not translate falls back to English. The
/// language is Windows' when available (English otherwise) unless one is chosen in Settings; FCT_LANG forces one for
/// tests. Texts are read when windows are built, so a change of language takes a restart.
/// </summary>
public static class L
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The language in use, either app-wide or, inside <see cref="Scope"/>, for one thread.</summary>
    private sealed record Active(string Code, CultureInfo Culture, LanguagePack? Pack)
    {
        public static Active For(string code) =>
            new(code, code == "es" ? Spanish : code == "en" ? English : LanguagePacks.Find(code)?.Culture ?? English, LanguagePacks.Find(code));
    }

    private static Active _app = Active.For(SystemCode);
    [ThreadStatic] private static Active? _thread;

    private static Active Now => _thread ?? _app;

    // ---- Which language -----------------------------------------------------------------------------------------------------

    /// <summary>Choosable languages, each named in itself: Spanish, English, then the files alphabetically.</summary>
    public static IReadOnlyList<(string Code, string Name)> Languages { get; } =
        new List<(string, string)> { ("es", "Español"), ("en", "English") }
            .Concat(LanguagePacks.All.Values.OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase).Select(p => (p.Code, p.Name)))
            .ToList();

    public static string Code => Now.Code;
    public static bool En => Code == "en";
    public static CultureInfo Culture => Now.Culture;

    /// <summary>Windows' language (or FCT_LANG), whatever was chosen in Settings.</summary>
    public static string SystemCode =>
        Supported(Environment.GetEnvironmentVariable("FCT_LANG") is { Length: > 0 } forced ? forced : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    /// <summary>
    /// The language a first run keeps in config.json: Windows' (English when FCT has no texts for it). Null when FCT_LANG
    /// forces one, so a test language is never saved.
    /// </summary>
    public static string? FirstRunCode =>
        Environment.GetEnvironmentVariable("FCT_LANG") is { Length: > 0 } ? null : Supported(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    private static string Supported(string wanted)
    {
        var code = wanted.Length < 2 ? "" : wanted[..2].ToLowerInvariant();
        return IsAvailable(code) ? code : "en";
    }

    /// <summary>Applies the Settings choice (null or unknown: Windows' language). FCT_LANG always wins.</summary>
    public static void Use(string? code)
    {
        var forced = Environment.GetEnvironmentVariable("FCT_LANG") is { Length: > 0 };
        _app = Active.For(!forced && code != null && IsAvailable(code) ? code : SystemCode);
    }

    /// <summary>
    /// Another language for what this thread produces until disposed (the stats page has its own language setting).
    /// Null or unknown means the app's.
    /// </summary>
    public static IDisposable Scope(string? code)
    {
        var outer = _thread;
        _thread = code != null && IsAvailable(code) ? Active.For(code) : null;
        return new Leave(outer);
    }

    private sealed class Leave(Active? outer) : IDisposable
    {
        public void Dispose() => _thread = outer;
    }

    private static bool IsAvailable(string code) => code is "es" or "en" || LanguagePacks.All.ContainsKey(code);

    // ---- Texts ----------------------------------------------------------------------------------------------------------------

    public static string T(string es, string en) => Translate(es, es, en);

    /// <summary>A long text: language files key it as "#" + <paramref name="id"/> instead of the whole Spanish text.</summary>
    public static string Big(string id, string es, string en) => Translate("#" + id, es, en);

    /// <summary><see cref="T"/> with {0}, {1}… filled in the language's culture.</summary>
    public static string F(string es, string en, params object?[] args) => string.Format(Culture, T(es, en), args);

    private static string Translate(string key, string es, string en)
    {
        var now = Now;
        if (now.Code == "es")
        {
            return es;
        }
        if (now.Code != "en" && now.Pack != null && now.Pack.Texts.TryGetValue(key, out var translated) && translated.Length > 0)
        {
            return translated;
        }
        return en;
    }

    /// <summary>Shown name of a container kind or treasure stored in data.json.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored ?? "";
        }
        return GameNames.TryGet(stored, out var names) ? T(names.Es, names.En) : stored;
    }

    // ---- Numbers and dates ------------------------------------------------------------------------------------------------------

    public static string N(int n) => n.ToString("0", Culture);

    /// <summary>"3 cofres" / "1 chest".</summary>
    public static string Count(int n, string esOne, string esMany, string enOne, string enMany) =>
        N(n) + " " + (n == 1 ? T(esOne, enOne) : T(esMany, enMany));

    /// <summary>"12.º", "12th", or the pattern of the language file ("{0}." without one).</summary>
    public static string Ordinal(int n)
    {
        switch (Code)
        {
            case "es":
                return n + ".º";
            case "en":
                var lastTwo = n % 100;
                var suffix = lastTwo is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
                return n + suffix;
            default:
                return string.Format(Culture, Now.Pack?.Ordinal ?? "{0}.", n);
        }
    }

    /// <summary>Short date and time of a match: "05/11 22:54" or "Nov 5 22:54".</summary>
    public static string DateTimeShort(DateTime when) => when.ToString(En ? "MMM d HH:mm" : "dd/MM HH:mm", Culture);
}
