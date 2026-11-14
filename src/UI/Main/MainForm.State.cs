using System.Drawing;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>Showing the monitor's state: every refresh comes here on the window's thread.</summary>
public sealed partial class MainForm
{
    private string? _profilesShown;
    private bool _pickingProfile;

    // What the window shows: kept here, as the controls' Visible is false while the window itself is hidden (started in
    // the background), which made it size itself without the team card and show up cut.
    private bool _teamShown, _noticeShown, _updateShown;

    private void OnChanged(MonitorState state)
    {
        _state = state;
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }
        try
        {
            BeginInvoke(new Action(() => ShowState(state)));
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // the window is closing, or has no handle yet
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_state != null)
        {
            ShowState(_state);
        }
    }

    private void ShowState(MonitorState state)
    {
        ShowMatch(state);
        ShowFigures(state);
        _overlayButton.Enabled = _monitor.OverlayEnabled;
        _overlayButton.Chip = _monitor.OverlayVisible ? L.T("● Activado", "● On") : L.T("Apagado", "Off");
        _overlayButton.ChipOn = _monitor.OverlayVisible;
        ShowTeam(state.Team);
        ShowProfiles(state);
        ShowStrip(state);
        FitHeight();
    }

    /// <summary>The total, and the current match (live) or the last one with its details.</summary>
    private void ShowMatch(MonitorState state)
    {
        var view = state.View;
        var stats = state.Stats;
        _total.Text = view.Total;
        var live = view.CurrentLive && view.Current != "…";
        _match.Text = view.Current ?? "—";
        _matchCaption.Text = live ? L.T("Partida actual", "Current match") : L.T("Última partida", "Last match");
        _matchCaption.ForeColor = live ? Theme.Mint : Theme.Ink;
        _matchNote.Text = live ? L.T("en directo", "live")
            : view.Current == null ? ""
            : view.Current == "1" ? L.T("cofre abierto", "chest opened") : L.T("cofres abiertos", "chests opened");
        _totalNote.Text = stats.HistorySince is { } since ? L.F("desde el {0:dd/MM/yy}", "since {0:dd/MM/yy}", since) : "";
        _badgeText.Text = "● " + (view.Badge ?? "").TrimStart('●', ' ');
        _badge.Visible = !string.IsNullOrEmpty(view.Badge);
        _matchDetails.Text = live ? L.T("En directo: se confirma al acabar la partida", "Live: confirmed when the match ends")
            : stats.LastMatch is { } last ? Details(last)
            : L.T("Aún no hay partidas: ¡a jugar!", "No matches yet: go play!");
    }

    /// <summary>"Top 3 · 4 elim. · 1 tesoro · 18 min" (a win is "Victoria"); short enough to fit the strip at its size.</summary>
    private static string Details(LastMatchSummary match)
    {
        var parts = new List<string>();
        if (match.Placement is { } place and > 0)
        {
            parts.Add(place == 1 ? L.T("Victoria", "Victory") : $"Top {place}");
        }
        if (match.Eliminations is { } eliminations)
        {
            parts.Add(L.F("{0} elim.", "{0} elims", eliminations));
        }
        parts.Add(L.Count(match.Treasures, "tesoro", "tesoros", "treasure", "treasures"));
        if (Pretty.Minutes(match.DurationSeconds) is { } minutes)
        {
            parts.Add(minutes);
        }
        return string.Join(" · ", parts);
    }

    /// <summary>The six figures and the week. Today's title carries today's matches (from the overlay's first row).</summary>
    private void ShowFigures(MonitorState state)
    {
        var stats = state.Stats;
        _today.Text = L.N(stats.Today);
        _todayCaption.Text = state.View.Rows.FirstOrDefault()?.Detail is { Length: > 0 } detail
            ? $"{L.T("Hoy", "Today")} · {detail.Split('·')[0].Trim()}"
            : L.T("Hoy", "Today");
        _bestDay.Text = stats.BestDay != null ? L.N(stats.BestDayChests) : "—";
        _bestDayCaption.Text = stats.BestDay is { } day ? $"{L.T("Mejor día", "Best day")} · {day:dd/MM}" : L.T("Mejor día", "Best day");
        _average.Text = stats.Matches > 0 ? stats.Average.ToString("0.0", L.Culture) : "—";
        _bestMatch.Text = stats.Matches > 0 ? L.N(stats.BestMatch) : "—";
        _matches.Text = L.N(stats.HistoryMatches + stats.EarlierMatches); // with those before FCT, like the chests total
        _treasures.Text = L.N(stats.Treasures);

        _week.SetDays(stats.LastDays);
        var weekChests = stats.LastDays.Sum(d => d.Chests);
        var played = stats.LastDays.Where(d => d.Chests > 0).ToList(); // the chart's dashed line: the days played (two or more)
        _weekNote.Text = weekChests == 0 ? ""
            : played.Count < 2 ? L.Count(weekChests, "cofre", "cofres", "chest", "chests")
            : $"{L.Count(weekChests, "cofre", "cofres", "chest", "chests")} · {L.F("media {0}", "avg {0}", L.N((int)Math.Round(played.Average(d => d.Chests))))}";
    }

    /// <summary>The team card; without team data (teammates off) it hides and the window shrinks back.</summary>
    private void ShowTeam(TeamSummary? team)
    {
        if (_teamShown != (team != null))
        {
            _teamShown = team != null;
            _team.Card.Visible = _teamShown;
            FitHeight();
        }
        if (team != null)
        {
            _team.Show(team);
        }
    }

    /// <summary>One account: its name. Several: a picker.</summary>
    private void ShowProfiles(MonitorState state)
    {
        var several = state.Profiles.Count >= 2;
        var current = state.Profiles.FirstOrDefault(p => p.Key == state.CurrentKey);
        _profiles.Visible = several;
        _profileName.Visible = !several && current != null;
        _profileCaption.Visible = _profileIcon.Visible = current != null || several;
        _profileName.Text = current?.Name ?? "";

        _pickingProfile = true;
        var signature = string.Join("|", state.Profiles.Select(p => p.Key));
        if (signature != _profilesShown)
        {
            _profilesShown = signature;
            _profiles.Items.Clear();
            foreach (var profile in state.Profiles)
            {
                _profiles.Items.Add(new ProfileItem(profile.Key, Profiles.Describe(profile)));
            }
        }
        for (var i = 0; i < _profiles.Items.Count; i++)
        {
            if (_profiles.Items[i] is ProfileItem item && item.Key == state.CurrentKey)
            {
                _profiles.SelectedIndex = i;
                break;
            }
        }
        _pickingProfile = false;
    }

    /// <summary>The bottom strip: a notice (download, error, replays loading) wins over the update button.</summary>
    private void ShowStrip(MonitorState state)
    {
        _keepOpenOffered = state.AutoCloseOffer;
        var notice = !string.IsNullOrEmpty(state.Notice);
        _updateShown = !notice && state.Update != null;
        _update.Visible = _updateShown;
        if (state.Update != null)
        {
            _update.Text = L.T("Instalar la actualización", "Install the update");
        }
        _notice.Text = state.Notice ?? "";
        _notice.Cursor = state.AutoCloseOffer ? Cursors.Hand : Cursors.Default;
        _noticeShown = notice;
        _noticeCard.Visible = notice;
    }

    /// <summary>The window's design height for what shows (team card, bottom strip), kept at the same scale.</summary>
    private void FitHeight()
    {
        var team = _teamShown;
        var strip = _noticeShown || _updateShown;
        var stripY = team ? StripYUnderTeam : StripY;
        _layout.MoveTo(_noticeCard, stripY);
        _layout.MoveTo(_update, stripY);
        var height = strip ? stripY + StripHeight + StripMargin : team ? TeamDesignHeight : PlainHeight;
        if (height != _layout.DesignHeight)
        {
            _layout.Resize(height);
        }
    }

    // ---- profile picker --------------------------------------------------------------------------------------------

    private ComboBox ProfilePicker(Point at, Size size)
    {
        var picker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Text(9f),
            Location = at,
            Size = size,
            Visible = false,
        };
        picker.DrawItem += DrawProfile;
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (!_pickingProfile && picker.SelectedItem is ProfileItem item)
            {
                _monitor.SetProfile(item.Key);
            }
        };
        return picker;
    }

    private void DrawProfile(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }
        var selected = e.State.HasFlag(DrawItemState.Selected) && !e.State.HasFlag(DrawItemState.ComboBoxEdit);
        using var back = new SolidBrush(selected ? Theme.GoldSoft : Theme.Panel);
        e.Graphics.FillRectangle(back, e.Bounds);
        TextRenderer.DrawText(e.Graphics, _profiles.Items[e.Index]?.ToString(), _profiles.Font, e.Bounds, selected ? Theme.GoldHi : Theme.Ink,
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private sealed record ProfileItem(string Key, string Text)
    {
        public override string ToString() => Text;
    }
}
