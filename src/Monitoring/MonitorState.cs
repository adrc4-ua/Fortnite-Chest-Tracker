namespace FCT.Monitoring;

// What the monitor hands to the main window each time something changes. Immutable snapshots: the window only reads
// them, on its own thread, so nothing here needs locking.

/// <summary>A full snapshot for the main window.</summary>
public sealed record MonitorState(
    OverlayView View,
    IReadOnlyList<Profile> Profiles,
    string? CurrentKey,
    UpdateInfo? Update,
    string? Notice,
    bool AutoCloseOffer,
    ChestStats Stats)
{
    /// <summary>Squad of the match in progress, or of the last one; null when teammates are turned off.</summary>
    public TeamSummary? Team { get; init; }

    /// <summary>Progress of an active estimation waiting for played matches ("2/5", or null).</summary>
    public string? Estimation => View.Estimation;
}

/// <summary>Figures of the profile on screen; a match in progress already counts in today's and the best match.</summary>
/// <param name="Matches">Finished matches counted.</param>
/// <param name="Average">Chests per finished match.</param>
/// <param name="BestMatch">Most chests in one match.</param>
/// <param name="Treasures">Treasures found.</param>
/// <param name="Today">Chests today.</param>
/// <param name="BestDay">Day with the most chests (null without matches).</param>
/// <param name="BestDayChests">Chests on <paramref name="BestDay"/>.</param>
public sealed record ChestStats(int Matches, double Average, int BestMatch, int Treasures, int Today, DateTime? BestDay, int BestDayChests)
{
    public static readonly ChestStats Empty = new(0, 0, 0, 0, 0, null, 0);

    /// <summary>The seven days of the chart, oldest first, today (with the match in progress) last.</summary>
    public IReadOnlyList<DayChests> LastDays { get; init; } = Array.Empty<DayChests>();

    /// <summary>Every match in the saved history, also those whose replay Fortnite has already deleted.</summary>
    public int HistoryMatches { get; init; }

    /// <summary>Matches before FCT, from the estimate of the earlier chests (added to the matches shown).</summary>
    public int EarlierMatches { get; init; }

    /// <summary>Initial chests before FCT.</summary>
    public int InitialChests { get; init; }

    /// <summary>Manual average entered by user, if any.</summary>
    public double? ManualAverage { get; init; }

    /// <summary>Local date of the oldest saved match (null when there is none).</summary>
    public DateTime? HistorySince { get; init; }

    /// <summary>The latest finished match of the profile (null when there is none).</summary>
    public LastMatchSummary? LastMatch { get; init; }
}

/// <summary>One day of the chart. <paramref name="Future"/>: a day of this week still to come.</summary>
public sealed record DayChests(DateTime Day, int Chests, bool Future = false);

/// <summary>The latest match: chests (broken ones included), how many of them were broken, treasures, place and eliminations.</summary>
public sealed record LastMatchSummary(int Chests, int Destroyed, int Treasures, int? Placement, int? Players, int? Eliminations, int? DurationSeconds = null);

/// <summary>
/// The squad card. <paramref name="Unknown"/>: the match was read before teammates were recorded, so there is
/// nothing to show about them.
/// </summary>
public sealed record TeamSummary(bool Live, int Mine, IReadOnlyList<TeamMember> Mates, bool Unknown)
{
    public int MatesTotal
    {
        get
        {
            var sum = 0;
            foreach (var mate in Mates)
            {
                sum += mate.Chests;
            }
            return sum;
        }
    }

    public int Total => Mine + MatesTotal;
}

/// <summary>A teammate and the chests seen (a minimum).</summary>
public sealed record TeamMember(string Name, int Chests);
