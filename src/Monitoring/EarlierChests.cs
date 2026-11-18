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
    private volatile IReadOnlyList<MatchRecord> _shownMatches = Array.Empty<MatchRecord>();

    public EarlierChests(SettingsGate settings, TrackerData data)
    {
        _settings = settings;
        _data = data;
    }

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
        HistoryEstimator.Compute(_shownMatches, totalMatches, DateTime.UtcNow, sample, manualAverage);

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
    /// estimate with that season total, taken now, applied once <paramref name="sample"/> matches give an average.
    /// </summary>
    public void SetEstimate(int? totalMatches, string source, int sample, double? manualAverage, string? playerName = null)
    {
        var shown = ShownKey;
        var key = shown ?? "";
        _settings.Edit(c =>
        {
            if (totalMatches is not { } total)
            {
                c.Estimates.Remove(key);
                c.InitialChests.Remove(key);
                return;
            }
            var name = !string.IsNullOrWhiteSpace(playerName) ? playerName : c.Estimates.GetValueOrDefault(key)?.PlayerName;
            c.Estimates[key] = new HistoryEstimate
            {
                TotalMatches = total,
                SnapshotUtc = DateTime.UtcNow,
                Source = source,
                ProfileKey = shown,
                SampleSize = sample,
                ManualAverage = manualAverage,
                PlayerName = name,
            };
            if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrEmpty(c.Profile))
            {
                c.Profile = name;
            }
        });
    }

    /// <summary>
    /// If an estimate was set up with a player name before replays (or under ""), and a replay arrives with
    /// a different player name, updates the profile and re-estimates via StatsClient API.
    /// If the API fails, keeps what was already estimated. True if anything was updated.
    /// </summary>
    public bool SyncWithPlayerIfMismatched()
    {
        var updated = false;
        var estimates = _settings.Read(c => c.Estimates.ToList());
        foreach (var (key, estimate) in estimates)
        {
            if (string.IsNullOrWhiteSpace(estimate.PlayerName))
            {
                continue;
            }

            var config = _settings.Read(c => c);
            var eventMatches = _data.Matches.Where(m => Profiles.IsEventMatch(m, config)).ToList();
            if (eventMatches.Count == 0)
            {
                continue;
            }

            // Find matching replay or newest replay if key is empty/placeholder
            var targetMatch = eventMatches.FirstOrDefault(m => Profiles.Key(m) == (estimate.ProfileKey ?? key))
                              ?? (string.IsNullOrEmpty(key) || key == estimate.PlayerName ? eventMatches.MaxBy(m => m.MatchStartUtc ?? m.AnalyzedAtUtc) : null);

            if (targetMatch == null || string.IsNullOrWhiteSpace(targetMatch.PlayerName))
            {
                continue;
            }

            var actualKey = Profiles.Key(targetMatch);
            var isMismatch = !string.Equals(estimate.PlayerName, targetMatch.PlayerName, StringComparison.OrdinalIgnoreCase);

            if (isMismatch)
            {
                var newName = targetMatch.PlayerName;
                estimate.PlayerName = newName;
                estimate.ProfileKey = actualKey;

                if (StatsClient.Available)
                {
                    try
                    {
                        var answer = StatsClient.TotalMatchesAsync(targetMatch.EpicId, newName).GetAwaiter().GetResult();
                        if (answer.Outcome == StatsClient.Outcome.Ok)
                        {
                            estimate.TotalMatches = answer.Matches;
                            estimate.Source = "api";
                            estimate.SnapshotUtc = DateTime.UtcNow;
                        }
                    }
                    catch
                    {
                        // Keep existing figures if API fails
                    }
                }

                _settings.Edit(c =>
                {
                    c.Profile = actualKey;
                    if (key != actualKey)
                    {
                        c.Estimates.Remove(key);
                        if (c.InitialChests.Remove(key, out var chests))
                        {
                            c.InitialChests[actualKey] = chests;
                        }
                    }
                    c.Estimates[actualKey] = estimate;

                    if (estimate.Done || estimate.ManualAverage != null)
                    {
                        var playerMatches = Profiles.MatchesOf(_data, c, actualKey);
                        var computed = HistoryEstimator.Compute(playerMatches, estimate.TotalMatches, estimate.SnapshotUtc, estimate.SampleSize, estimate.ManualAverage);
                        if (computed.Chests is { } newChests)
                        {
                            if (newChests > 0)
                            {
                                c.InitialChests[actualKey] = newChests;
                            }
                            else
                            {
                                c.InitialChests.Remove(actualKey);
                            }
                        }
                    }
                });
                updated = true;
            }
            else if (key != actualKey && (string.IsNullOrEmpty(key) || key == estimate.PlayerName))
            {
                estimate.ProfileKey = actualKey;
                _settings.Edit(c =>
                {
                    c.Profile = actualKey;
                    c.Estimates.Remove(key);
                    c.Estimates[actualKey] = estimate;
                    if (c.InitialChests.Remove(key, out var chests))
                    {
                        c.InitialChests[actualKey] = chests;
                    }
                });
                updated = true;
            }
        }
        return updated;
    }

    /// <summary>
    /// Turns every pending estimate that can be computed into its profile's number, each with that profile's own matches.
    /// Returns the notices to show (one per estimate applied).
    /// </summary>
    public List<string> ApplyPending()
    {
        SyncWithPlayerIfMismatched();
        var notices = new List<string>();
        var pending = _settings.Read(c => c.Estimates.Where(e => !e.Value.Done).Select(e => (e.Key, e.Value)).ToList());
        foreach (var (entry, estimate) in pending)
        {
            // "" was asked before the first match: it belongs to the profile there is now (that match's).
            var key = entry.Length > 0 ? entry : _settings.Read(c => Profiles.Current(_data, c)?.Key);
            if (key == null)
            {
                continue;
            }
            var matches = _settings.Read(c => Profiles.MatchesOf(_data, c, key));
            if (HistoryEstimator.Compute(matches, estimate.TotalMatches, estimate.SnapshotUtc, estimate.SampleSize, estimate.ManualAverage).Chests is not { } chests)
            {
                continue; // not enough matches for an average yet
            }
            _settings.Edit(c =>
            {
                if (chests > 0)
                {
                    c.InitialChests[key] = chests;
                }
                else
                {
                    c.InitialChests.Remove(key);
                }
                estimate.ProfileKey = key;
                estimate.Done = true;
                if (entry != key)
                {
                    c.Estimates.Remove(entry);
                    c.Estimates[key] = estimate;
                }
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
