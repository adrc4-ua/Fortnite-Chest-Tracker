using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// Cómo contar las partidas de antes de FCT: solo las guardadas (repeticiones), o calcular también las demás
/// (media de cofres × partidas que FCT no tiene). En dos pasos:
/// <list type="number">
/// <item>Tu nombre de Epic: se buscan tus partidas de esta temporada (fortnite-api.com). Solo si no se puede (perfil
/// privado, nombre no encontrado, sin conexión o versión sin clave) se pide escribirlas a mano mirando fortnite.gg.</item>
/// <item>Cuántos cofres abres por partida: lo calcula FCT con tus próximas partidas (5 por defecto, 10 recomendado)
/// junto a las guardadas, o lo escribes tú.</item>
/// </list>
/// Abajo, el resultado al momento.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EstimateDialog : ThemedForm
{
    /// <summary>Lo elegido: <see cref="Estimate"/> false = solo las partidas guardadas.</summary>
    public sealed record Choice(bool Estimate, int TotalMatches, string Source, int Sample, double? ManualAverage);

    private const int W = 480, Pad = 20, Inner = W - 2 * Pad - 28;

    private readonly PillButton _onlyReplays;
    private readonly PillButton _withEstimate;
    private readonly Card _panel;
    private readonly Label _step1;
    private readonly Card _nameBox;
    private readonly TextBox _name;
    private readonly PillButton _search;
    private readonly IconTile _statusIcon;
    private readonly Label _status;
    private readonly Panel _manual;
    private readonly Label _manualText;
    private readonly Card _totalBox;
    private readonly TextBox _total;
    private readonly PillButton _site;
    private readonly Label _step2;
    private readonly PillButton _calc;
    private readonly PillButton _typed;
    private readonly Panel _calcArea;
    private readonly Panel _typedArea;
    private readonly List<PillButton> _samples = new();
    private readonly TextBox _average;
    private readonly Card _result;
    private readonly Label _resultValue;
    private readonly Label _resultText;
    private readonly PillButton _ok;
    private readonly PillButton _cancel;
    private readonly Func<int, int, double?, HistoryEstimator.Result>? _previewOf;
    private readonly string? _accountId;
    private readonly string? _originalName;
    private bool _estimate;
    private bool _manualAverage;
    private bool _manualTotal;   // se muestran los campos para escribir las partidas a mano
    private bool _found;         // las partidas vienen de la búsqueda
    private bool _statusShown;   // línea de resultado de la búsqueda (Visible no sirve: es false hasta que se ve la ventana)
    private int _sample;
    private string _source = "manual";

    /// <param name="fromScratch">No hay partidas guardadas que contar (no había, o se eligió «Solo las nuevas»): la primera
    /// opción es «Empezar de cero» y los textos no hablan de partidas guardadas.</param>
    /// <param name="recalculating">From Settings → Recalculate: the window is "Estimate" and goes straight to the
    /// calculation (no choice between counting earlier matches or not: Cancel keeps everything as it was).</param>
    private EstimateDialog(string? playerName, string? accountId, HistoryEstimate? current, Func<int, int, double?, HistoryEstimator.Result>? previewOf, bool owned,
        bool fromScratch = false, bool recalculating = false)
    {
        _previewOf = previewOf;
        _accountId = accountId;
        _originalName = playerName;
        Text = recalculating ? L.T("Estimación", "Estimate") : L.F("Cofres antes de {0}", "Chests before {0}", AppInfo.ShortName);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = !owned;
        StartPosition = owned ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
        Icon = AppIcon.Load();

        // ---- Cabecera ------------------------------------------------------------------------------------------------
        Controls.Add(new IconTile { IconSvg = Icons.Calc, Solid = true, Location = new Point(Pad, Pad), Size = new Size(42, 42) });
        var y = AddText(this, L.T("¿Sumamos tus partidas de antes?", "Add your earlier matches too?"), Theme.Text(12f, FontStyle.Bold), Theme.GoldHi, 74, Pad - 1, W - 74 - Pad) + 2;
        y = AddText(this, fromScratch
                ? L.T("FCT empieza a contar desde ahora. Si quieres, calculamos cuántos cofres abriste antes.",
                    "FCT starts counting from now. If you like, we'll work out how many chests you opened before.")
                : L.T("FCT solo ve las partidas que tienes guardadas. Si quieres, calculamos cuántos cofres abriste en las demás.",
                    "FCT only sees the matches you have saved. If you like, we'll work out how many chests you opened in the rest."),
            Theme.Text(9f), Theme.Muted, 74, y, W - 74 - Pad);
        y = Math.Max(y, Pad + 42) + 16;

        var half = (W - 2 * Pad - 8) / 2;
        _onlyReplays = fromScratch
            ? Pill(this, L.T("Empezar de cero", "Start from zero"), new Point(Pad, y), new Size(half, 38), 9f, Icons.Sparkle)
            : Pill(this, L.T("Solo las guardadas", "Only saved ones"), new Point(Pad, y), new Size(half, 38), 9f, Icons.Folder);
        _withEstimate = Pill(this, L.T("Calcular partidas previas", "Work out earlier matches"), new Point(Pad + half + 8, y), new Size(half, 38), 9f, Icons.Calc);
        _onlyReplays.Click += (_, _) => SelectMode(false);
        _withEstimate.Click += (_, _) => SelectMode(true);
        if (recalculating)
        {
            _onlyReplays.Visible = _withEstimate.Visible = false; // straight to the calculation
        }
        else
        {
            y += 38 + 14;
        }

        _panel = new Card { Location = new Point(Pad, y), Size = new Size(W - 2 * Pad, 100), Radius = 16, BackColor = Theme.Card };
        Controls.Add(_panel);

        // ---- 1 · Partidas de esta temporada: se buscan por el nombre de Epic --------------------------------------
        _step1 = Title(_panel, StatsClient.Available
            ? L.T("1 · ¿Cómo te llamas en Epic?", "1 · What's your Epic name?")
            : L.T("1 · ¿Cuántas partidas llevas esta temporada?", "1 · How many matches this season?"));
        _nameBox = FieldBox(_panel, Inner - 108, 36);
        _name = FieldText(_nameBox, playerName ?? "", Theme.Text(10f), Theme.Ink);
        SetCue(_name, L.T("Tu nombre de Epic", "Your Epic name"));
        _name.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LookUp();
            }
        };
        _search = Pill(_panel, L.T("Buscar", "Look up"), Point.Empty, new Size(100, 36), 9f, Icons.Search);
        _search.BackColor = Theme.Accent;
        _search.HoverColor = Theme.AccentHover;
        _search.Click += async (_, _) => await LookUp();
        _statusIcon = new IconTile { Size = new Size(24, 24), Visible = false };
        _panel.Controls.Add(_statusIcon);
        _status = new Label { AutoSize = true, MaximumSize = new Size(Inner - 34, 0), Font = Theme.Text(8.75f, FontStyle.Bold), ForeColor = Theme.GoldHi, BackColor = Color.Transparent, UseMnemonic = false, Visible = false };
        _panel.Controls.Add(_status);

        // A mano (solo si no se pudieron buscar): el número de fortnite.gg.
        _manual = new Panel { BackColor = Color.Transparent, Size = new Size(Inner, 80), Visible = false };
        _panel.Controls.Add(_manual);
        _manualText = new Label
        {
            AutoSize = true, MaximumSize = new Size(Inner, 0), Font = Theme.Text(8.5f), ForeColor = Theme.Muted, BackColor = Color.Transparent, UseMnemonic = false,
            Text = L.T("Escríbelas tú: en fortnite.gg busca tu nombre y mira las partidas de esta temporada.",
                "Type them in yourself: look up your name on fortnite.gg and check this season's matches."),
        };
        _manual.Controls.Add(_manualText);
        _totalBox = FieldBox(_manual, 140, 40);
        _total = FieldText(_totalBox, current is { TotalMatches: > 0 } c ? c.TotalMatches.ToString(CultureInfo.InvariantCulture) : "", Theme.Display(14f, FontStyle.Bold), Theme.GoldBase);
        SetCue(_total, L.T("Partidas", "Matches"));
        _total.MaxLength = 7;
        _total.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
        _total.TextChanged += (_, _) =>
        {
            if (_manualTotal)
            {
                _source = "manual";
                _found = false;
            }
            UpdatePreview();
        };
        _site = Pill(_manual, L.T("Abrir fortnite.gg", "Open fortnite.gg"), Point.Empty, new Size(150, 40), 8.75f, Icons.External);
        _site.Click += (_, _) => OpenSite();

        // ---- 2 · Cofres por partida -------------------------------------------------------------------------------
        _step2 = Title(_panel, L.T("2 · ¿Cuántos cofres abres por partida?", "2 · How many chests do you open per match?"));
        var halfInner = (Inner - 8) / 2;
        _calc = Pill(_panel, L.T("Que lo calcule FCT", "Let FCT work it out"), Point.Empty, new Size(halfInner, 32), 8.75f);
        _typed = Pill(_panel, L.T("Lo sé, lo escribo", "I know it, I'll type it"), Point.Empty, new Size(halfInner, 32), 8.75f);
        _calc.Click += (_, _) => SelectAverage(false);
        _typed.Click += (_, _) => SelectAverage(true);

        _calcArea = new Panel { Size = new Size(Inner, 64), BackColor = Color.Transparent };
        AddText(_calcArea, L.T("Con tus próximas", "With your next"), Theme.Text(8.75f), Theme.Muted, 0, 8, 110);
        var sx = 112;
        var sw = (Inner - sx - 3 * 6) / HistoryEstimator.SampleChoices.Length;
        foreach (var n in HistoryEstimator.SampleChoices)
        {
            var label = n == HistoryEstimator.RecommendedSample ? L.F("{0} ★", "{0} ★", n) : n.ToString(CultureInfo.InvariantCulture);
            var pill = Pill(_calcArea, label, new Point(sx, 0), new Size(sw, 32), 9f);
            pill.Tag = n;
            pill.Click += (_, _) => SelectSample((int)pill.Tag!);
            _samples.Add(pill);
            sx += sw + 6;
        }
        AddText(_calcArea, fromScratch
                ? L.F("partidas. Cuantas más, más exacto (te recomendamos {0} ★).", "matches. The more, the more accurate (we recommend {0} ★).", HistoryEstimator.RecommendedSample)
                : L.F("partidas. Cuantas más, más exacto (te recomendamos {0} ★). También usamos las que ya tienes guardadas.",
                    "matches. The more, the more accurate (we recommend {0} ★). The ones you already have saved count too.", HistoryEstimator.RecommendedSample),
            Theme.Text(8.25f), Theme.Muted, 0, 40, Inner);
        _panel.Controls.Add(_calcArea);

        _typedArea = new Panel { Size = new Size(Inner, 64), BackColor = Color.Transparent, Visible = false };
        var averageBox = FieldBox(_typedArea, 140, 40);
        averageBox.Location = Point.Empty;
        _average = FieldText(averageBox, current?.ManualAverage is double typed ? typed.ToString("0.##", L.Culture) : "", Theme.Display(14f, FontStyle.Bold), Theme.GoldBase);
        SetCue(_average, "18,5");
        _average.MaxLength = 6;
        _average.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.';
        _average.TextChanged += (_, _) => UpdatePreview();
        AddText(_typedArea, L.T("cofres por partida, de media.", "chests per match, on average."), Theme.Text(8.75f), Theme.Muted, 152, 11, Inner - 152);
        _panel.Controls.Add(_typedArea);

        // ---- Resultado ------------------------------------------------------------------------------------------
        _result = new Card { Size = new Size(Inner, 64), Radius = 14, BackColor = Theme.GoldSoft, Edge = Theme.GoldLine };
        _result.Controls.Add(new IconTile { IconSvg = Icons.Chest, Location = new Point(12, 13), Size = new Size(38, 38) });
        _resultValue = new FitLabel { Gold = true, Font = Theme.Display(17f, FontStyle.Bold), BackColor = Color.Transparent, Location = new Point(60, 7), Size = new Size(Inner - 72, 30), UseMnemonic = false };
        _resultText = new FitLabel { Font = Theme.Text(8.25f), ForeColor = Theme.Champ, BackColor = Color.Transparent, Location = new Point(61, 37), Size = new Size(Inner - 72, 18), UseMnemonic = false };
        _result.Controls.Add(_resultValue);
        _result.Controls.Add(_resultText);
        _panel.Controls.Add(_result);

        // ---- Botones. Primer uso: «Continuar» y sin «Cancelar» (cerrar la ventana cierra la app). ------------------
        _ok = Pill(this, owned ? L.T("Guardar", "Save") : L.T("Continuar", "Continue"), Point.Empty, new Size(130, 38), 9f);
        _ok.BackColor = Theme.Accent;
        _ok.HoverColor = Theme.AccentHover;
        _ok.Click += (_, _) => Save();
        _cancel = Pill(this, L.T("Cancelar", "Cancel"), Point.Empty, new Size(130, 38), 9f);
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Visible = owned;
        if (owned)
        {
            CancelButton = _cancel;
        }

        // Estado inicial: lo ya guardado (encontrado o escrito) o vacío.
        if (current is { TotalMatches: > 0 })
        {
            _source = current.Source;
            if (current.Source == "api" && StatsClient.Available)
            {
                _found = true;
                ShowStatus(true, L.F("{0} esta temporada", "{0} this season", L.Count(current.TotalMatches, "partida", "partidas", "match", "matches")));
            }
            else
            {
                _manualTotal = true;
            }
        }
        _manualTotal |= !StatsClient.Available;
        _sample = current?.SampleSize is int s && HistoryEstimator.SampleChoices.Contains(s) ? s : HistoryEstimator.DefaultSample;
        SelectSample(_sample);
        SelectAverage(current?.ManualAverage != null);
        SelectMode(recalculating || current != null);
    }

    /// <summary>Muestra el diálogo. Devuelve lo elegido, o null si se cancela o se cierra la ventana.</summary>
    /// <param name="previewOf">Resultado con (partidas de la temporada, partidas para la media, media escrita); null en el primer uso.</param>
    /// <param name="fromScratch">Sin partidas guardadas que contar: «Empezar de cero» en vez de «Solo las guardadas».</param>
    /// <param name="recalculating">Settings → Recalculate: titled "Estimate", straight to the calculation.</param>
    public static Choice? Ask(IWin32Window? owner, string? playerName, string? accountId, HistoryEstimate? current, Func<int, int, double?, HistoryEstimator.Result>? previewOf,
        bool fromScratch = false, bool recalculating = false)
    {
        using var dialog = new EstimateDialog(playerName, accountId, current, previewOf, owner != null, fromScratch, recalculating);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Result : null;
    }

    private Choice? Result { get; set; }

    private int Total => int.TryParse(_total.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private double? TypedAverage =>
        double.TryParse(_average.Text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var a) && a > 0 && a < 1000 ? a : null;

    /// <summary>Coloca las piezas del panel según lo que se ve (búsqueda, a mano, cálculo o media escrita).</summary>
    private void Arrange()
    {
        SuspendLayout();
        var x = 14;
        var py = 14;
        _step1.Location = new Point(x, py);
        py += _step1.PreferredHeight + 8;

        var search = StatsClient.Available;
        _nameBox.Visible = _search.Visible = search;
        _step2.Visible = _calc.Visible = _typed.Visible = true;
        if (search)
        {
            _nameBox.Location = new Point(x, py);
            _search.Location = new Point(x + Inner - 100, py);
            py += 36 + 10;
        }
        _statusIcon.Visible = _status.Visible = _statusShown;
        if (_statusShown)
        {
            _statusIcon.Location = new Point(x, py);
            _status.Location = new Point(x + 32, py + Math.Max(0, (24 - _status.PreferredHeight) / 2));
            py += Math.Max(24, _status.PreferredHeight) + 10;
        }
        _manual.Visible = _manualTotal;
        if (_manualTotal)
        {
            // Sin búsqueda en esta versión: no hace falta decir «escríbelas tú» con un motivo; solo cómo.
            _manualText.Location = new Point(0, 0);
            var my = _manualText.PreferredHeight + 8;
            _totalBox.Location = new Point(0, my);
            _site.Location = new Point(_totalBox.Right + 10, my);
            _manual.Height = my + 40;
            _manual.Location = new Point(x, py);
            py += _manual.Height + 12;
        }

        py += 4;
        _step2.Location = new Point(x, py);
        py += _step2.PreferredHeight + 8;
        var halfInner = (Inner - 8) / 2;
        _calc.Location = new Point(x, py);
        _typed.Location = new Point(x + halfInner + 8, py);
        py += 32 + 12;
        _calcArea.Location = _typedArea.Location = new Point(x, py);
        _calcArea.Height = _calcArea.Controls.Cast<Control>().Max(c => c.Bottom);
        _typedArea.Height = 42;
        py += (_manualAverage ? _typedArea.Height : _calcArea.Height) + 12;

        _result.Location = new Point(x, py);
        py += _result.Height + 14;
        _panel.Height = py;

        var bottom = (_estimate ? _panel.Bottom : _onlyReplays.Bottom) + 18;
        _ok.Location = new Point(W - Pad - 130, bottom);
        _cancel.Location = new Point(W - Pad - 130 - 8 - 130, bottom);
        ClientSize = new Size(W, bottom + 38 + Pad);
        ResumeLayout();
        Invalidate(true);
    }

    private void SelectMode(bool estimate)
    {
        _estimate = estimate;
        Mark(_onlyReplays, !estimate);
        Mark(_withEstimate, estimate);
        _panel.Visible = estimate;
        UpdatePreview();
        Arrange();
    }

    private void SelectAverage(bool typed)
    {
        _manualAverage = typed;
        Mark(_calc, !typed);
        Mark(_typed, typed);
        _calcArea.Visible = !typed;
        _typedArea.Visible = typed;
        UpdatePreview();
        Arrange();
    }

    private void SelectSample(int n)
    {
        _sample = n;
        foreach (var p in _samples)
        {
            Mark(p, (int)p.Tag! == n);
        }
        UpdatePreview();
    }

    /// <summary>El resultado: la cifra en grande y, debajo, de dónde sale (o qué falta).</summary>
    private void UpdatePreview()
    {
        if (!_estimate)
        {
            _ok.Enabled = true;
            return;
        }
        var total = Total;
        var typed = _manualAverage ? TypedAverage : null;
        _ok.Enabled = total > 0 && (!_manualAverage || typed != null);
        void Show(string value, string text)
        {
            _resultValue.Text = value;
            _resultText.Text = text;
        }
        string Matches(int n) => L.Count(n, "partida", "partidas", "match", "matches");
        if (total <= 0 && _found)
        {
            Show("0", L.T("Esta temporada no hay partidas de antes que sumar.", "There are no earlier matches to add this season."));
            return;
        }
        if (total <= 0)
        {
            Show("—", StatsClient.Available && !_manualTotal
                ? L.T("Primero busca tu nombre de Epic.", "First, look up your Epic name.")
                : L.T("Primero escribe tus partidas de esta temporada.", "First, type your matches this season."));
            return;
        }
        if (_manualAverage && typed == null)
        {
            Show("—", L.T("Escribe cuántos cofres abres por partida.", "Type how many chests you open per match."));
            return;
        }
        if (_previewOf == null)
        {
            // Primer uso: aún no se han leído las partidas guardadas.
            Show(L.T("Pendiente", "Pending"), typed != null
                ? L.T("Lo calculamos en cuanto leamos tus partidas guardadas.", "We'll work it out as soon as we read your saved matches.")
                : L.F("Lo calculamos tras tus próximas {0}.", "We'll work it out after your next {0}.", Matches(_sample)));
            return;
        }
        var r = _previewOf(total, _sample, typed);
        string Avg(double v) => v.ToString(r.RoundedDown ? "0" : "0.#", L.Culture);
        var chestsWord = L.T("cofres de antes", "earlier chests");
        if (r.Chests is int chests)
        {
            Show($"≈ {chests.ToString("#,0", L.Culture)} {chestsWord}", typed != null
                ? L.F("Con tu media de {0} por partida.", "With your average of {0} per match.", Avg(typed.Value))
                : L.F("Con una media de {0} por partida.", "With an average of {0} per match.", Avg(r.Average ?? 0)));
            return;
        }
        if (r.Provisional is int provisional)
        {
            Show($"≈ {provisional.ToString("#,0", L.Culture)} {chestsWord}",
                L.F("De momento. Se ajusta tras tus próximas {0}.", "For now. It's fine-tuned after your next {0}.", Matches(r.Waiting)));
            return;
        }
        Show(L.T("Pendiente", "Pending"), L.F("Lo calculamos tras tus próximas {0}.", "We'll work it out after your next {0}.", Matches(r.Waiting)));
    }

    /// <summary>Busca las partidas de esta temporada por el nombre de Epic. Si no se puede, explica por qué y pide escribirlas.</summary>
    private async Task LookUp()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || !_search.Enabled)
        {
            _name.Focus();
            return;
        }
        _search.Enabled = false;
        _search.Text = L.T("Buscando…", "Looking up…");
        // Con el nombre sin tocar se busca por la cuenta (no depende de cómo se escriba el nombre).
        var id = string.Equals(_name.Text.Trim(), _originalName, StringComparison.OrdinalIgnoreCase) ? _accountId : null;
        var answer = await StatsClient.TotalMatchesAsync(id, _name.Text);
        _search.Enabled = true;
        _search.Text = L.T("Buscar", "Look up");
        if (answer.Outcome == StatsClient.Outcome.Ok)
        {
            _found = true;
            _manualTotal = false;
            _total.Text = answer.Matches.ToString(CultureInfo.InvariantCulture);
            _source = "api";
            ShowStatus(true, answer.Matches > 0
                ? L.F("{0}: {1} esta temporada", "{0}: {1} this season", answer.Name ?? _name.Text.Trim(), L.Count(answer.Matches, "partida", "partidas", "match", "matches"))
                : L.F("{0}: aún no ha jugado esta temporada", "{0}: no matches this season yet", answer.Name ?? _name.Text.Trim()));
        }
        else
        {
            _found = false;
            _manualTotal = true;
            _total.Text = "";
            ShowStatus(false, answer.Outcome switch
            {
                StatsClient.Outcome.Private => L.T("Tu perfil de Fortnite es privado, así que no podemos ver tus partidas.", "Your Fortnite profile is private, so we can't see your matches."),
                StatsClient.Outcome.NotFound => L.T("No encontramos a nadie con ese nombre. Revísalo o escríbelas tú.", "We couldn't find anyone with that name. Check it, or type them in yourself."),
                StatsClient.Outcome.Busy => L.T("Hay muchas búsquedas ahora mismo. Prueba en un minuto o escríbelas tú.", "Lots of lookups right now. Try again in a minute, or type them in yourself."),
                _ => L.T("Ahora mismo no podemos buscarlas.", "We can't look them up right now."),
            });
        }
        UpdatePreview();
        Arrange();
    }

    private void ShowStatus(bool ok, string text)
    {
        _statusIcon.IconSvg = ok ? Icons.Check : Icons.Warning;
        _statusShown = true;
        _status.ForeColor = ok ? Theme.GoldHi : Theme.Champ;
        _status.Text = text;
        _statusIcon.Invalidate();
    }

    private void OpenSite()
    {
        var name = _name.Text.Trim();
        var url = name.Length > 0 ? "https://fortnite.gg/stats?player=" + Uri.EscapeDataString(name) : "https://fortnite.gg/stats";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch
        {
            // sin navegador: se puede abrir a mano
        }
    }

    private void Save()
    {
        var typed = _manualAverage ? TypedAverage : null;
        if (_estimate && (Total <= 0 || (_manualAverage && typed == null)))
        {
            return;
        }
        Result = new Choice(_estimate, _estimate ? Total : 0, _found ? "api" : "manual", _sample, typed);
        DialogResult = DialogResult.OK;
    }

    private static void Mark(PillButton b, bool on)
    {
        b.BackColor = on ? Theme.Accent : Theme.Panel;
        b.HoverColor = on ? Theme.AccentHover : Theme.PanelHover;
        b.ForeColor = on ? Theme.AccentInk : Theme.Ink;
        b.Invalidate();
    }

    private static PillButton Pill(Control parent, string text, Point at, Size size, float font, string? icon = null)
    {
        var b = new PillButton { Text = text, Location = at, Size = size, Radius = 11, Font = Theme.Text(font, FontStyle.Bold), IconSvg = icon };
        parent.Controls.Add(b);
        return b;
    }

    private static Label Title(Control parent, string text)
    {
        var label = new Label { Text = text, Font = Theme.Text(9.75f, FontStyle.Bold), ForeColor = Theme.Ink, BackColor = Color.Transparent, AutoSize = true, MaximumSize = new Size(Inner, 0), UseMnemonic = false };
        parent.Controls.Add(label);
        return label;
    }

    /// <summary>Fondo liso y redondeado de un campo de texto (sin velo: así el cuadro de texto no se nota).</summary>
    private static Card FieldBox(Control parent, int width, int height)
    {
        var box = new Card { Size = new Size(width, height), Radius = 10, BackColor = Theme.Bg2, Edge = Theme.Line, Flat = true };
        parent.Controls.Add(box);
        return box;
    }

    private static TextBox FieldText(Card box, string text, Font font, Color color)
    {
        var t = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Bg2, ForeColor = color, Font = font, Text = text, Width = box.Width - 22 };
        t.Location = new Point(11, (box.Height - t.Height) / 2);
        box.Controls.Add(t);
        return t;
    }

    /// <summary>Texto gris de ayuda dentro de un campo vacío (EM_SETCUEBANNER).</summary>
    private static void SetCue(TextBox box, string text) =>
        box.HandleCreated += (_, _) => SendMessage(box.Handle, 0x1501, (IntPtr)1, text);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);

    private static int AddText(Control parent, string text, Font font, Color color, int x, int y, int width)
    {
        // La etiqueta se mide sola (con el ancho máximo): así nunca corta la última línea.
        var label = new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, Location = new Point(x, y), AutoSize = true, MaximumSize = new Size(width, 0), UseMnemonic = false };
        parent.Controls.Add(label);
        return y + label.PreferredHeight + 2;
    }
}
