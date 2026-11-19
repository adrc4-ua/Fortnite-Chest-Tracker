namespace FCT.Monitoring;

/// <summary>
/// Several accounts on one PC: when a new match (live or just finished) belongs to another account that already has
/// counted matches, its profile is shown. Only a new match switches, so a profile picked by hand stays until someone plays.
/// </summary>
internal sealed class PlayerFollower
{
    private readonly TrackerData _data;
    private readonly Tracker _tracker;
    private readonly Func<Config> _config;
    private string? _lastSeen; // "account|session|file" of the newest match

    public PlayerFollower(TrackerData data, Tracker tracker, Func<Config> config)
    {
        _data = data;
        _tracker = tracker;
        _config = config;
    }

    /// <summary>The profile to switch to (the player who just played), or null to stay.</summary>
    public Profile? NewPlayer()
    {
        var newest = _tracker.LiveMatch ?? _data.Matches.Where(m => Profiles.IsEventMatch(m, _config())).MaxBy(m => m.MatchStartUtc ?? m.AnalyzedAtUtc);
        if (newest == null)
        {
            return null;
        }
        var account = Profiles.Key(newest);
        var seen = $"{account}|{newest.SessionId}|{newest.ReplayFile}";
        var first = _lastSeen == null; // at start, what was already there does not change the chosen profile
        var same = seen == _lastSeen;
        _lastSeen = seen;
        if (first || same || string.IsNullOrEmpty(account))
        {
            return null;
        }
        var config = _config();
        if (Profiles.Current(_data, config)?.Key == account)
        {
            return null;
        }
        // An account without counted matches yet is followed once its first match is counted.
        return Profiles.List(_data, config).FirstOrDefault(p => p.Key == account);
    }
}
