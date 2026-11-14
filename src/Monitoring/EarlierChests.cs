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
    public void SetEstimate(int? totalMatches, string source, int sample, double? manualAverage)
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
            c.Estimates[key] = new HistoryEstimate { TotalMatches = total, SnapshotUtc = DateTime.UtcNow, Source = source, ProfileKey = shown, SampleSize = sample, ManualAverage = manualAverage };
        });
    }

    /// <summary>
    /// Turns every pending estimate that can be computed into its profile's number, each with that profile's own matches.
    /// Returns the notices to show (one per estimate applied).
    /// </summary>
    public List<string> ApplyPending()
    {
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
