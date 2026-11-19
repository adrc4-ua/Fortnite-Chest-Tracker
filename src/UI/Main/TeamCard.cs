using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// The "Equipo" card of the main window (with teammates on): the player's chests and those each teammate was seen
/// opening in the current or last match. Teammates' figures are a minimum: the replay only holds what happens nearby.
/// <para>
/// One fixed layout per team size, all built up front (the window's <see cref="ProportionalLayout"/> scales design
/// places, so nothing is moved later): alone, the card splits in two halves like the main card (you, and why there are
/// no teammates); with teammates, the width is shared in equal centred columns with a divider between them.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TeamCard
{
    private const int MaxPlayers = 4; // the player and up to three teammates (squads)
    private const int Inset = 8, Pad = 6, NameY = 33, NameHeight = 14, ValueY = 47, ValueHeight = 26, DividerY = 36;

    /// <summary>The labels of one team size: a name and a figure per column, and the dividers between them.</summary>
    private sealed class Layout
    {
        public FitLabel[] Names = Array.Empty<FitLabel>();
        public FitLabel[] Values = Array.Empty<FitLabel>();
        public Control[] Parts = Array.Empty<Control>();
    }

    private readonly SectionTitle _title;
    private readonly FitLabel _note;
    private readonly Layout[] _layouts = new Layout[MaxPlayers];
    private readonly FitLabel _soloCaption, _soloText;

    public TeamCard(Point at, int width, int height)
    {
        Card = new Card { Location = at, Size = new Size(width, height), Visible = false };
        _title = new SectionTitle { Text = L.T("Equipo", "Team"), Location = new Point(12, 7), Size = new Size(260, 20) };
        Card.Controls.Add(_title);
        _note = FitLabel.Add(Card, "", 7.75f, FontStyle.Regular, Theme.Muted, new Point(width - 14 - 270, 9), new Size(270, 16), ContentAlignment.MiddleRight);

        for (var players = 1; players <= MaxPlayers; players++)
        {
            // alone: two halves, the player's own figure on the left and the reason on the right
            _layouts[players - 1] = Columns(width, height, players == 1 ? 2 : players, players == 1 ? 1 : players);
        }

        // the right half of the solo layout: a caption like a column's name and a short text in place of a figure
        var half = (width - 2 * Inset) / 2;
        _soloCaption = FitLabel.Add(Card, L.T("Compañeros", "Teammates"), 7.75f, FontStyle.Regular, Theme.Muted,
            new Point(Inset + half + Pad, NameY), new Size(half - 2 * Pad, NameHeight), ContentAlignment.TopCenter);
        _soloText = FitLabel.Add(Card, "", 10.5f, FontStyle.Bold, Theme.Ink,
            new Point(Inset + half + Pad, ValueY), new Size(half - 2 * Pad, ValueHeight), ContentAlignment.MiddleCenter);
        var solo = _layouts[0];
        solo.Parts = solo.Parts.Append(_soloCaption).Append(_soloText).ToArray();

        foreach (var layout in _layouts)
        {
            SetVisible(layout, false);
        }
    }

    public Card Card { get; }

    public void Show(TeamSummary team)
    {
        _title.Text = team.Live ? L.T("Equipo · partida actual", "Team · current match") : L.T("Equipo · última partida", "Team · last match");
        _title.ForeColor = team.Live ? Theme.Mint : Theme.Ink;

        var mates = team.Unknown ? 0 : Math.Min(team.Mates.Count, MaxPlayers - 1);
        var players = mates + 1;
        for (var i = 0; i < MaxPlayers; i++)
        {
            SetVisible(_layouts[i], i == players - 1);
        }

        var layout = _layouts[players - 1];
        layout.Names[0].Text = L.T("Tú", "You");
        layout.Values[0].Text = L.N(team.Mine);
        for (var i = 1; i < players; i++)
        {
            var mate = team.Mates[i - 1];
            layout.Names[i].Text = mate.Name;
            layout.Values[i].Text = "≥ " + L.N(mate.Chests);
        }

        if (team.Unknown)
        {
            _soloText.Text = L.T("Sin datos", "No data");
            _note.Text = L.T("Partida analizada antes de guardar compañeros", "Match analyzed before teammates were saved");
        }
        else if (mates == 0)
        {
            _soloText.Text = L.T("Partida en solitario", "Solo match");
            _note.Text = team.Live ? L.T("Sin compañeros por ahora", "No teammates so far") : L.T("Jugaste sin compañeros", "You played without teammates");
        }
        else
        {
            var total = team.Total;
            _note.Text = total > 0
                ? L.F("Equipo ≥ {0} · compañeros ≥ {1} %", "Team ≥ {0} · teammates ≥ {1} %", L.N(total), (100.0 * team.MatesTotal / total).ToString("0", L.Culture))
                : L.F("Equipo ≥ {0}", "Team ≥ {0}", L.N(total));
        }
    }

    /// <summary>
    /// <paramref name="slots"/> equal columns across the card with a divider between each pair; the first
    /// <paramref name="filled"/> get a centred name and figure (the player's in gold).
    /// </summary>
    private Layout Columns(int width, int height, int slots, int filled)
    {
        var inner = width - 2 * Inset;
        var parts = new List<Control>();
        var names = new FitLabel[filled];
        var values = new FitLabel[filled];
        for (var i = 0; i < slots; i++)
        {
            var left = Inset + inner * i / slots;
            var right = Inset + inner * (i + 1) / slots;
            if (i > 0)
            {
                var divider = new Panel { BackColor = Theme.Line, Location = new Point(left, DividerY), Size = new Size(1, height - DividerY - 8) };
                Card.Controls.Add(divider);
                parts.Add(divider);
            }
            if (i >= filled)
            {
                continue;
            }
            names[i] = FitLabel.Add(Card, "", 7.75f, FontStyle.Regular, Theme.Muted,
                new Point(left + Pad, NameY), new Size(right - left - 2 * Pad, NameHeight), ContentAlignment.TopCenter);
            values[i] = FitLabel.Add(Card, "", 14f, FontStyle.Bold, Theme.Ink,
                new Point(left + Pad, ValueY), new Size(right - left - 2 * Pad, ValueHeight), ContentAlignment.MiddleCenter, figure: true, gold: i == 0);
            parts.Add(names[i]);
            parts.Add(values[i]);
        }
        return new Layout { Names = names, Values = values, Parts = parts.ToArray() };
    }

    private static void SetVisible(Layout layout, bool visible)
    {
        foreach (var part in layout.Parts)
        {
            part.Visible = visible;
        }
    }
}
