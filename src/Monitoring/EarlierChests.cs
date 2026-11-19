using FCT.App;

namespace FCT.Monitoring;

/// <summary>
/// Chests opened before using FCT, per profile: the number itself and the estimate that produces it (season matches ×
/// average chests), which waits until enough matches are counted to give an average.
/// </summary>
internal sealed class EarlierChests
{
    private readonly SettingsGate _settings;
    private readonly TrackerData _data;
    private readonly Func<int> _replayFiles;
    private volatile IReadOnlyList<MatchRecord> _shownMatches = Array.Empty<MatchRecord>();

    /// <param name="replayFiles">How many .replay files the folder holds now (any mode or account): at the cap, the
    /// history is cut (<see cref="HistoryEstimator.IsCut"/>).</param>
    public EarlierChests(SettingsGate settings, TrackerData data, Func<int>? replayFiles = null)
    {
        _settings = settings;
        _data = data;
        _replayFiles = replayFiles ?? (() => 0);
    }

    private bool FolderFull() => HistoryEstimator.IsCut(_replayFiles());

    /// <summary>Key of the profile on screen ("" before the first match), kept up to date by the monitor.</summary>
    public string? ShownKey { get; set; }

    /// <summary>The counted matches of the profile on screen, for the estimate preview.</summary>
    public void ShowMatches(IReadOnlyList<MatchRecord> matches) => _shownMatches = matches;

    public int Chests => _settings.Read(c => Profiles.InitialChests(c, ShownKey));

    public HistoryEstimate? Estimate => _settings.Read(c => c.Estimates.GetValueOrDefault(ShownKey ?? ""));

    /// <summary>
    /// Matches before FCT for the profile on screen: those of its season total (from the stats or typed) that FCT has no
    /// replay of. Counted as soon as the estimate is chosen (first start or Recalculate), without waiting for the
    /// average; 0 without an estimate ("start from zero", or only a typed number of chests).
    /// </summary>
    public int Matches(IReadOnlyList<MatchRecord> matches) => _settings.Read(c => Profiles.EarlierMatches(c, ShownKey, matches));

    /// <summary>What the estimate would give with this season total, with the matches counted right now.</summary>
    public HistoryEstimator.Result Preview(int totalMatches, int sample, double? manualAverage) =>
        HistoryEstimator.Compute(_shownMatches, totalMatches, DateTime.UtcNow, sample, manualAverage, FolderFull());

    /// <summary>Types the number by hand (0 removes it).</summary>
    public void SetChests(int count)
    {
        var key = ShownKey ?? "";
        _settings.Edit(c =>
        {
            if (count > 0)
            {
                c.InitialChests[key] = count;
            }
            else
            {
                c.InitialChests.Remove(key);
            }
        });
    }

    /// <summary>
    /// Null <paramref name="totalMatches"/>: only the saved replays count (estimate and number removed). Otherwise an
    /// estimate with that season total, taken now, applied once <paramref name="sample"/> matches give an average (at
    /// once with a typed average, or with enough saved matches). Called from the window: it only reads the matches of
    /// the profile on screen as the monitor last published them, never the history itself.
    /// </summary>
    /// <param name="otherProfile">Another player's name typed on purpose (Settings): never checked against the replays.</param>
    /// <param name="replaysComing">Saved replays are about to be read (Recalculate → "Count them"): the matches on
    /// screen still lack them, so it is left pending, applied once they are read, as at the first start.</param>
    public void SetEstimate(int? totalMatches, string source, int sample, double? manualAverage, string? playerName = null, bool otherProfile = false,
        bool replaysComing = false)
    {
        var shown = ShownKey;
        var key = shown ?? "";
        var shownMatches = _shownMatches;
        var cut = FolderFull();
        _settings.Edit(c =>
        {
            if (totalMatches is not { } total)
            {
                c.Estimates.Remove(key);
                c.InitialChests.Remove(key);
                // one asked before the first match would end up on this profile
                c.Estimates.Remove("");
                c.InitialChests.Remove("");
                return;
            }
            var name = !string.IsNullOrWhiteSpace(playerName) ? playerName.Trim() : c.Estimates.GetValueOrDefault(key)?.PlayerName;
            // Without a profile yet it is filed under the Epic name typed (shown as the profile until the first match;
            // the replays then move it to the real account), or under "" without a name.
            var target = key.Length > 0 ? key : name ?? "";
            var estimate = new HistoryEstimate
            {
                TotalMatches = total,
                SnapshotUtc = DateTime.UtcNow,
                Source = source,
                ProfileKey = shown,
                SampleSize = sample,
                ManualAverage = manualAverage,
                PlayerName = name,
                OtherProfile = otherProfile,
                HistoryCut = cut,
            };
            var matches = target == shown ? shownMatches : Array.Empty<MatchRecord>();
            if (!replaysComing && HistoryEstimator.Compute(matches, total, estimate.SnapshotUtc, sample, manualAverage, cut).Chests is { } chests)
            {
                estimate.Done = true;
                SetNumber(c, target, chests);
            }
            else
            {
                c.InitialChests.Remove(target);
            }
            c.Estimates[target] = estimate;
            if (target.Length > 0)
            {
                // anything left under "" (asked before the first match) is replaced by this one
                c.Estimates.Remove("");
                c.InitialChests.Remove("");
            }
            if (key.Length == 0 && name != null)
            {
                c.Profile = name;
            }
        });
    }

    private static DateTime Start(MatchRecord m) => m.MatchStartUtc ?? m.AnalyzedAtUtc;

    private static bool SameName(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void SetNumber(Config c, string key, int chests)
    {
        if (chests > 0)
        {
            c.InitialChests[key] = chests;
        }
        else
        {
            c.InitialChests.Remove(key);
        }
    }

    /// <summary>A failed lookup (no connection, too many lookups) is tried again after this long (shorter in tests).</summary>
    internal static TimeSpan RetryAfter { get; set; } = DefaultRetryAfter;

    internal static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(5);

    // Lookups of the replays' name going on (by "account|name"), and when a failed one may be tried again. Only used on
    // the monitor thread; a lookup never holds it up: it is looked at again on the next rounds until it answers.
    private readonly Dictionary<string, Task<StatsClient.Answer>> _lookups = new();
    private readonly Dictionary<string, DateTime> _retryAt = new();
    private string? _checked; // what the last check saw (matches, estimates): skipped while equal

    /// <summary>
    /// The Epic name typed for an estimate against the replays: the replays' name wins. An estimate filed before the
    /// first match (under the name typed, or "") moves to the account with that name, or, when the name was typed
    /// wrong, to the account in the replays. When the replays' name is a different one, the season total is asked again
    /// with it (fortnite-api.com): found, the estimate is redone for that player; not found or private, the figures stay
    /// as the player entered them; no answer (no connection, too many lookups), it is asked again a few minutes later.
    /// Another profile's name chosen on purpose in Settings (<see cref="HistoryEstimate.OtherProfile"/>) is left alone.
    /// </summary>
    private void FollowReplayNames()
    {
        DropMirrors();
        var named = _settings.Read(c => c.Estimates.Where(e => !string.IsNullOrWhiteSpace(e.Value.PlayerName)).Select(e => (e.Key, e.Value)).ToList());
        if (named.Count == 0)
        {
            return;
        }
        var config = _settings.Read(c => c);
        var stamp = $"{_data.Matches.Count}|{config.CountRanked}|"
                    + string.Join(";", named.Select(e => $"{e.Key}:{e.Value.PlayerName}:{e.Value.ProfileKey}:{e.Value.OtherProfile}"));
        var now = DateTime.UtcNow;
        if (stamp == _checked && _lookups.Count == 0 && !_retryAt.Values.Any(at => at <= now))
        {
            return; // nothing new since the last check
        }
        foreach (var due in _retryAt.Where(r => r.Value <= now).Select(r => r.Key).ToList())
        {
            _retryAt.Remove(due); // its time came: asked again below (if it is still needed)
        }
        var played = _data.Matches.Where(m => !string.IsNullOrWhiteSpace(m.PlayerName) && Profiles.IsEventMatch(m, config)).ToList();
        if (played.Count == 0)
        {
            _checked = stamp;
            return;
        }
        var newestOf = played.GroupBy(Profiles.Key).ToDictionary(g => g.Key, g => g.MaxBy(Start)!);
        foreach (var (entry, estimate) in named)
        {
            string account;
            if (newestOf.ContainsKey(entry))
            {
                if (estimate.OtherProfile)
                {
                    continue; // another player's matches, on purpose
                }
                account = entry;
            }
            else
            {
                // filed before the first match: the account that played with that name, else the one the estimate was
                // asked for, else (the name was typed wrong) the account in the replays
                account = played.Where(m => SameName(m.PlayerName, estimate.PlayerName)).MaxBy(Start) is { } byName ? Profiles.Key(byName)
                    : estimate.ProfileKey is { } asked && newestOf.ContainsKey(asked) ? asked
                    : Profiles.Key(played.MaxBy(Start)!);
            }
            var newest = newestOf[account];
            var replayName = newest.PlayerName!.Trim();
            var renamed = !SameName(replayName, estimate.PlayerName);
            if (account == entry && !renamed)
            {
                continue;
            }

            // The replays' name settles it once the stats answered for it (or there is no lookup in this version).
            StatsClient.Answer? answer = null;
            var settled = !renamed || !StatsClient.Available;
            if (!settled)
            {
                var id = account + "|" + replayName;
                if (!_lookups.TryGetValue(id, out var lookup) && !_retryAt.ContainsKey(id))
                {
                    _lookups[id] = lookup = StatsClient.TotalMatchesAsync(newest.EpicId, replayName);
                }
                if (lookup != null)
                {
                    if (!lookup.IsCompleted)
                    {
                        continue; // it answers in the background: looked at again next round
                    }
                    _lookups.Remove(id);
                    answer = lookup.IsCompletedSuccessfully ? lookup.Result : new StatsClient.Answer(StatsClient.Outcome.Failed);
                    settled = answer.Outcome is StatsClient.Outcome.Ok or StatsClient.Outcome.NotFound or StatsClient.Outcome.Private;
                    if (settled)
                    {
                        _retryAt.Remove(id);
                    }
                    else
                    {
                        _retryAt[id] = now + RetryAfter;
                    }
                }
                if (!settled && account == entry)
                {
                    continue; // nothing to change until the stats answer
                }
            }

            int? total = null;
            if (answer is { Outcome: StatsClient.Outcome.Ok } found)
            {
                // Today's stats also count the matches played since the estimate was taken (its new matches): taken off,
                // so the total stays the one at the moment of the estimate and its progress is kept.
                var since = _settings.Read(c => Profiles.MatchesOf(_data, c, account)).Count(m => Start(m) > estimate.SnapshotUtc);
                total = Math.Max(0, found.Matches - since);
            }

            _settings.Edit(c =>
            {
                if (entry != account)
                {
                    c.Estimates.Remove(entry);
                    var moved = c.InitialChests.Remove(entry, out var number);
                    c.Profile = account; // the profile typed is this account
                    if (c.Estimates.ContainsKey(account))
                    {
                        return; // the account already has its own estimate: that one stays
                    }
                    if (moved)
                    {
                        SetNumber(c, account, number);
                    }
                }
                if (settled)
                {
                    estimate.PlayerName = replayName; // until then the typed name stays, so it is asked again
                }
                estimate.ProfileKey = account;
                if (total is { } t)
                {
                    estimate.TotalMatches = t;
                    estimate.Source = "api";
                }
                c.Estimates[account] = estimate;
                if (estimate.Done && HistoryEstimator.Compute(Profiles.MatchesOf(_data, c, account), estimate.TotalMatches, estimate.SnapshotUtc,
                        estimate.SampleSize, estimate.ManualAverage, estimate.HistoryCut).Chests is { } chests)
                {
                    SetNumber(c, account, chests);
                }
            });
        }
        // the next rounds skip all this until a match or an estimate changes, a lookup answers or a retry is due
        _checked = stamp;
    }

    /// <summary>
    /// Versions 5.12.x also filed every estimate (and its number) under "" as a copy: that copy would show on every
    /// other profile. Removed once.
    /// </summary>
    private void DropMirrors()
    {
        static bool Mirrored(Config c) => c.Estimates.TryGetValue("", out var copy)
            && c.Estimates.Any(e => e.Key.Length > 0 && e.Value.SnapshotUtc == copy.SnapshotUtc && e.Value.TotalMatches == copy.TotalMatches);
        if (!_settings.Read(Mirrored))
        {
            return;
        }
        _settings.Edit(c =>
        {
            if (Mirrored(c))
            {
                c.Estimates.Remove("");
                c.InitialChests.Remove("");
            }
        });
    }

    /// <summary>
    /// Turns every pending estimate that can be computed into its profile's number, each with that profile's own matches.
    /// Returns the notices to show (one per estimate applied). Only on the monitor thread.
    /// </summary>
    public List<string> ApplyPending()
    {
        FollowReplayNames();
        var notices = new List<string>();
        var pending = _settings.Read(c => c.Estimates.Where(e => !e.Value.Done).Select(e => (e.Key, e.Value)).ToList());
        foreach (var (entry, estimate) in pending)
        {
            // "" was asked before the first match: it belongs to the profile there is now (that match's)
            var key = entry.Length > 0 ? entry : _settings.Read(c => Profiles.Current(_data, c)?.Key) ?? "";
            var matches = _settings.Read(c => Profiles.MatchesOf(_data, c, key));
            if (HistoryEstimator.Compute(matches, estimate.TotalMatches, estimate.SnapshotUtc, estimate.SampleSize, estimate.ManualAverage, estimate.HistoryCut).Chests is not { } chests)
            {
                continue; // not enough matches for an average yet
            }
            _settings.Edit(c =>
            {
                if (entry != key)
                {
                    c.Estimates.Remove(entry);
                    c.InitialChests.Remove(entry);
                }
                SetNumber(c, key, chests);
                if (key.Length > 0)
                {
                    estimate.ProfileKey = key;
                }
                estimate.Done = true;
                c.Estimates[key] = estimate;
            });
            notices.Add(Notice(key, chests));
        }
        return notices;
    }

    private string Notice(string key, int chests)
    {
        if (chests <= 0)
        {
            return L.T("Partidas de antes: tus repeticiones ya las cubren todas, no hace falta estimar.", "Earlier matches: your replays already cover them all, no estimate needed.");
        }
        var amount = chests.ToString("#,0", L.Culture);
        var profiles = _settings.Read(c => Profiles.List(_data, c));
        var name = profiles.Count > 1 ? profiles.FirstOrDefault(p => p.Key == key)?.Name : null; // several players: say whose
        return name == null
            ? L.F("Partidas de antes: ≈ {0} cofres estimados sumados a tu total.", "Earlier matches: ≈ {0} estimated chests added to your total.", amount)
            : L.F("Partidas de antes de {1}: ≈ {0} cofres estimados sumados a su total.", "{1}'s earlier matches: ≈ {0} estimated chests added to their total.", amount, name);
    }
}
