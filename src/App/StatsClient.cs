using System.Reflection;
using System.Text.Json;

namespace FCT.App;

/// <summary>
/// Battle Royale matches of a player in the current season, from fortnite-api.com (public stats). Only called when the player asks
/// for it (estimate of the chests before FCT). It needs the project's key, put in the exe when it is built (GitHub
/// secret FORTNITE_API_KEY; for tests, the variable FCT_FORTNITE_API_KEY). Without a key the player types the number.
/// </summary>
public static class StatsClient
{
    /// <summary>
    /// What fortnite-api.com answered (checked by hand, 2026-11-12):
    /// <list type="bullet">
    /// <item>200: the stats. The name does not care about case; spaces around it make it «not found» (it is trimmed).
    /// An account id must be lower case: the same id in upper case (as replays write it) is «not found» (2026-11-16).</item>
    /// <item>403 «stats are not public»: <see cref="Private"/>.</item>
    /// <item>404 «account does not exist»: <see cref="NotFound"/>. 404 «didnt play any match yet» (the account exists
    /// but has no match in the window, e.g. none this season): <see cref="Ok"/> with 0 matches.</item>
    /// <item>429: too many lookups (180 a minute per key, shared by every copy of the app): <see cref="Busy"/>.</item>
    /// <item>401: missing or wrong key: <see cref="NoKey"/>. Anything else, or no connection: <see cref="Failed"/>.</item>
    /// </list>
    /// </summary>
    public enum Outcome { Ok, Private, NotFound, NoKey, Busy, Failed }

    public sealed record Answer(Outcome Outcome, int Matches = 0, string? Name = null);

    private static string? Key =>
        Environment.GetEnvironmentVariable("FCT_FORTNITE_API_KEY") is { Length: > 0 } test ? test
        : typeof(StatsClient).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "FortniteApiKey")?.Value is { Length: > 0 } key ? key : null;

    /// <summary>True when this build can look the matches up by itself.</summary>
    public static bool Available => Key != null;

    /// <summary>Matches of the player in the current season (timeWindow=season): by account id when known, else by Epic name.</summary>
    public static async Task<Answer> TotalMatchesAsync(string? accountId, string? name)
    {
        if (Key is not { } key)
        {
            return new Answer(Outcome.NoKey);
        }
        var cleanName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var byId = accountId is { Length: 32 } && accountId.All(Uri.IsHexDigit);
        if (!byId && cleanName == null)
        {
            return new Answer(Outcome.NotFound);
        }
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.ShortName}/{AppInfo.VersionText}");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", key);

            if (byId)
            {
                var idAnswer = await FetchAsync(http, $"https://fortnite-api.com/v2/stats/br/v2/{accountId!.ToLowerInvariant()}?timeWindow=season");
                if (idAnswer.Outcome is not Outcome.NotFound || cleanName == null)
                {
                    return idAnswer;
                }
            }

            return cleanName != null
                ? await FetchAsync(http, $"https://fortnite-api.com/v2/stats/br/v2?name={Uri.EscapeDataString(cleanName)}&accountType=epic&timeWindow=season")
                : new Answer(Outcome.NotFound);
        }
        catch
        {
            return new Answer(Outcome.Failed);
        }
    }

    private static async Task<Answer> FetchAsync(HttpClient http, string url)
    {
        using var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ErrorText(response);
            return (int)response.StatusCode switch
            {
                403 => new Answer(Outcome.Private),
                // The account exists, it just has no match in the window (this season): 0, not «not found».
                404 when error.Contains("didnt play", StringComparison.OrdinalIgnoreCase) || error.Contains("didn't play", StringComparison.OrdinalIgnoreCase)
                    => new Answer(Outcome.Ok, 0, null),
                404 => new Answer(Outcome.NotFound),
                429 => new Answer(Outcome.Busy),
                401 => new Answer(Outcome.NoKey),
                _ => new Answer(Outcome.Failed),
            };
        }
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        // Sin partidas esta temporada, "overall" llega vacío (null): son 0.
        var matches = data.TryGetProperty("stats", out var stats) && stats.TryGetProperty("all", out var all)
            && all.ValueKind == JsonValueKind.Object && all.TryGetProperty("overall", out var overall)
            && overall.ValueKind == JsonValueKind.Object && overall.TryGetProperty("matches", out var count) ? count.GetInt32() : 0;
        var shown = data.TryGetProperty("account", out var account) && account.TryGetProperty("name", out var n) ? n.GetString() : null;
        return new Answer(Outcome.Ok, matches, shown);
    }

    /// <summary>The «error» text of an error answer ({"status":404,"error":"..."}), or "".</summary>
    private static async Task<string> ErrorText(HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }
}
