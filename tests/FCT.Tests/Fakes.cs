using FCT.App;
using FCT.Data;
using FCT.Monitoring;

// Static state (fortnite-api.com stand-in, the OBS server): the tests run one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FCT.Tests;

/// <summary>Fake history and settings for the tests, in a temporary folder deleted afterwards.</summary>
internal sealed class Fakes : IDisposable
{
    public const string Version = "++Fortnite+Release-43.10";

    public Fakes()
    {
        Folder = Path.Combine(Path.GetTempPath(), "fct-tests-" + Guid.NewGuid().ToString("N"));
        Store = new Store(Folder);
        Gate = new SettingsGate(Store, Config);
        Earlier = new EarlierChests(Gate, Data, () => ReplayFiles);
        // no test reaches the real service: each one says what it answers (by default, nobody found)
        Lookups = 0;
        StatsClient.TestAnswer = (_, _) =>
        {
            Lookups++;
            return new StatsClient.Answer(StatsClient.Outcome.NotFound);
        };
    }

    public string Folder { get; }
    public Store Store { get; }
    public Config Config { get; } = new();
    public TrackerData Data { get; } = new();
    public SettingsGate Gate { get; }
    public EarlierChests Earlier { get; }

    /// <summary>.replay files in the (imaginary) replays folder.</summary>
    public int ReplayFiles { get; set; }

    /// <summary>Lookups made to the fortnite-api.com stand-in.</summary>
    public int Lookups { get; private set; }

    /// <summary>The stand-in answers this many matches this season (counting the lookups).</summary>
    public void ApiAnswers(int matches) => StatsClient.TestAnswer = (_, name) =>
    {
        Lookups++;
        return new StatsClient.Answer(StatsClient.Outcome.Ok, matches, name);
    };

    /// <summary>The stand-in answers this outcome (counting the lookups).</summary>
    public void ApiFails(StatsClient.Outcome outcome) => StatsClient.TestAnswer = (_, _) =>
    {
        Lookups++;
        return new StatsClient.Answer(outcome);
    };

    /// <summary>A Battle Royale match of this season that counts.</summary>
    public static MatchRecord Match(string account, string name, DateTime startUtc, int chests) => new()
    {
        SessionId = Guid.NewGuid().ToString("N"),
        ReplayFile = Guid.NewGuid().ToString("N") + ".replay",
        EpicId = account,
        PlayerName = name,
        MatchStartUtc = startUtc,
        AnalyzedAtUtc = startUtc,
        Playlist = "Playlist_DefaultSolo",
        GameVersion = Version,
        ChestsOpened = chests,
        JumpedFromBus = true,
    };

    /// <summary>A 32-hex Epic account id from a digit ("1" → "1111…").</summary>
    public static string Account(char digit) => new(digit, 32);

    /// <summary>Publishes the profile on screen to the estimate, as the monitor does.</summary>
    public void Show(string? key)
    {
        Earlier.ShownKey = key;
        Earlier.ShowMatches(key == null ? Array.Empty<MatchRecord>() : Analysis.Profiles.MatchesOf(Data, Config, key));
    }

    public void Dispose()
    {
        StatsClient.TestAnswer = null;
        EarlierChests.RetryAfter = EarlierChests.DefaultRetryAfter;
        try
        {
            Directory.Delete(Folder, true);
        }
        catch (IOException)
        {
        }
    }
}
