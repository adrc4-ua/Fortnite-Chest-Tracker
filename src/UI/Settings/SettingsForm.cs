using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using static FCT.UI.SettingsParts;

namespace FCT.UI;

/// <summary>
/// Settings in the stats page's style: a header with the gold icon tile, two columns of cards with diamond titles,
/// switches and gold buttons. Every change is applied and saved at once, and the stats page shares them: a change made
/// there shows here at once.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class SettingsForm : ThemedForm
{
    /// <summary>The update modes as offered: the config value, the button and the line under the buttons.</summary>
    private static readonly (string Mode, string Label, string Hint)[] UpdateChoices =
    {
        ("prompt", L.T("Preguntar", "Ask"), L.T("Te avisamos cuando salga una versión nueva", "We'll tell you when a new version is out")),
        ("auto", L.T("Automáticas", "Automatic"), L.T("Se instalan solas, nunca durante una partida", "They install by themselves, never during a match")),
        ("off", L.T("Nunca", "Never"), L.T("No se buscan versiones nuevas", "New versions aren't checked")),
    };

    // Two columns like the main window, under a 32 px header.
    private const int LeftX = 15, LeftWidth = 267, RightX = 297, RightWidth = 280, Header = 32;
    private const int MaxEarlierChests = 1_000_000;

    private readonly AppMonitor _monitor;
    private readonly Store _store;
    private Label _backupLine = null!;
    private PillButton _restore = null!;
    private string? _backupShown;
    private readonly ToggleSwitch _withFortnite, _openStats, _mates, _addMates, _ranked, _ammo, _food, _cannons;
    private readonly PillButton[] _updateButtons = new PillButton[UpdateChoices.Length];
    private readonly Label _updateHint;
    private readonly TextBox _folder, _earlier;
    private bool _loading;

    public SettingsForm(AppMonitor monitor, Store store)
    {
        _monitor = monitor;
        _store = store;
        var config = monitor.Config;
        Text = L.T("Configuración", "Settings");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(592, 531 + Header);
        Icon = AppIcon.Load();

        // ---- left column: options, what else counts, the replays folder
        var options = Card(this, LeftX, Header + 40, LeftWidth, 170, 16);
        Title(options, L.T("Opciones", "Options"));
        _withFortnite = SwitchRow(options, L.T("Abrir con Fortnite", "Open with Fortnite"), L.T("Se abre solo cuando juegas", "Opens by itself when you play"), 30, config.StartWithFortnite);
        _withFortnite.CheckedChanged += (_, _) => WithFortniteChanged();
        _openStats = SwitchRow(options, L.T("Abrir estadísticas al iniciar", "Open stats at startup"), null, 70, config.OpenStatsOnStart);
        _openStats.CheckedChanged += (_, _) => Saved(() => _monitor.Change(c => c.OpenStatsOnStart = _openStats.Checked));
        _mates = SwitchRow(options, L.T("Compañeros en la ventana", "Teammates in the window"), L.T("Sus cofres, si andan cerca de ti", "Their chests, if they're near you"), 98, config.ShowTeammates);
        _mates.CheckedChanged += (_, _) => TeammatesChanged(_mates);
        _addMates = SwitchRow(options, L.T("Sumar sus cofres a tu total", "Add their chests to your total"), null, 138, config.CountTeammateChests);
        _addMates.CheckedChanged += (_, _) => AddTeammatesChanged(_addMates);

        // Ranked matches, and what else counts as a chest (broken chests always count); only when switched on
        var extras = Card(this, LeftX, Header + 219, LeftWidth, 145, 16);
        Title(extras, L.T("Contar también", "Also count"));
        _ranked = SwitchRow(extras, L.T("Partidas clasificatorias", "Ranked matches"), null, 31, config.CountRanked);
        _ranked.CheckedChanged += (_, _) => Saved(() => _monitor.SetCountRanked(_ranked.Checked));
        _ammo = SwitchRow(extras, L.T("Cajas de munición", "Ammo boxes"), null, 60, config.CountAmmoBoxes);
        _ammo.CheckedChanged += (_, _) => Saved(() => _monitor.SetCountExtra(c => c.CountAmmoBoxes = _ammo.Checked));
        _food = SwitchRow(extras, L.T("Cajas de comida", "Food boxes"), null, 89, config.CountFoodBoxes);
        _food.CheckedChanged += (_, _) => Saved(() => _monitor.SetCountExtra(c => c.CountFoodBoxes = _food.Checked));
        _cannons = SwitchRow(extras, L.T("Cañones de botín", "Loot cannons"), null, 118, config.CountLootCannons);
        _cannons.CheckedChanged += (_, _) => Saved(() => _monitor.SetCountExtra(c => c.CountLootCannons = _cannons.Checked));

        // ---- right column: updates (three pills)
        var updates = Card(this, RightX, Header + 185, RightWidth, 116, 14);
        Title(updates, L.T("Actualizaciones", "Updates"));
        var pillWidth = (RightWidth - 26 - 2 * 6) / 3;
        for (var i = 0; i < UpdateChoices.Length; i++)
        {
            var index = i;
            _updateButtons[i] = Button(UpdateChoices[i].Label, new Point(13 + i * (pillWidth + 6), 36), new Size(pillWidth, 34), 10);
            _updateButtons[i].Click += (_, _) => ShowUpdateMode(index, save: true);
            updates.Controls.Add(_updateButtons[i]);
        }
        _updateHint = SettingsParts.Text(updates, "", 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 80), new Size(RightWidth - 26, 18), ContentAlignment.MiddleCenter);
        ShowUpdateMode(Math.Max(0, Array.FindIndex(UpdateChoices, c => c.Mode == config.UpdateMode)), save: false);

        var folder = Card(this, LeftX, Header + 373, LeftWidth, 94, 14);
        Title(folder, L.T("Carpeta de replays", "Replays folder"));
        _folder = Field(folder, new Point(13, 30), LeftWidth - 26 - 72, config.DemosFolder);
        var browse = Button(L.T("Cambiar", "Change"), new Point(LeftWidth - 13 - 66, 30), new Size(66, 30), 10);
        browse.Click += (_, _) => ChooseFolder();
        folder.Controls.Add(browse);
        SettingsParts.Text(folder, L.T("Tus datos se guardan en ", "Your data is saved in ") + store.Folder, 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 64), new Size(LeftWidth - 26, 28));

        _earlier = EarlierChests(monitor);
        ObsCard();
        BackupCard();
        BottomButtons(out var close);
        HeaderRow(config.Language);

        ShrinkToFit(this);
        _monitor.Changed += OnMonitorChanged;
        FormClosed += (_, _) => _monitor.Changed -= OnMonitorChanged;
        AcceptButton = close;
        CancelButton = close;
        ActiveControl = close; // no focus ring on the first switch when it opens
    }

    /// <summary>
    /// Chests from before FCT: the figure, editable between − and +, and the same calculation as the first start below.
    /// </summary>
    private TextBox EarlierChests(AppMonitor monitor)
    {
        var card = Card(this, RightX, Header + 40, RightWidth, 136, 16);
        Title(card, L.F("Cofres antes de {0}", "Chests before {0}", AppInfo.ShortName), 120);
        SettingsParts.Text(card, L.T("se suman a tu total", "added to your total"), 7.5f, FontStyle.Regular, Theme.Muted, new Point(RightWidth - 13 - 120, 9), new Size(120, 16), ContentAlignment.MiddleRight);
        const int StepWidth = 42, RowY = 36, RowHeight = 42;
        var minus = Button("−", new Point(13, RowY), new Size(StepWidth, RowHeight), 11, 12f);
        var plus = Button("+", new Point(RightWidth - 13 - StepWidth, RowY), new Size(StepWidth, RowHeight), 11, 12f);
        var back = new Card { Location = new Point(13 + StepWidth + 6, RowY), Size = new Size(RightWidth - 26 - 2 * (StepWidth + 6), RowHeight), Radius = 11, BackColor = Theme.Bg2, Edge = Theme.Line, Flat = true };
        var figure = new TextBox
        {
            BorderStyle = BorderStyle.None, BackColor = Theme.Bg2, ForeColor = Theme.GoldBase, Font = Theme.Display(16f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center, Width = back.Width - 20, MaxLength = 7,
            Text = Math.Min(MaxEarlierChests, monitor.InitialChests).ToString(L.Culture),
        };
        figure.Location = new Point(10, (back.Height - figure.Height) / 2);
        back.Controls.Add(figure);
        card.Controls.Add(back);
        plus.Click += (_, _) => StepEarlier(10);
        minus.Click += (_, _) => StepEarlier(-10);
        card.Controls.Add(plus);
        card.Controls.Add(minus);
        // "Recalcular": the calculation is done (or can be redone) here; its dialog is the first start's
        var recalculate = Button(L.T("Recalcular", "Recalculate"), new Point(13, RowY + RowHeight + 10), new Size(RightWidth - 26, 34), 11, 8.75f);
        recalculate.IconSvg = Icons.Calc;
        recalculate.Click += (_, _) => Recalculate();
        card.Controls.Add(recalculate);
        figure.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
        figure.TextChanged += (_, _) => Saved(() => _monitor.SetInitialChests(EarlierValue()));
        return figure;
    }

    /// <summary>The OBS page: its address with a copy button, and whether it is on.</summary>
    private void ObsCard()
    {
        var card = Card(this, RightX, Header + 310, RightWidth, 96, 14);
        Title(card, L.T("Overlay para OBS", "Overlay for OBS"), 92);
        var url = ObsServer.Url;
        var on = url != null;
        var state = new Card { Location = new Point(RightWidth - 13 - 78, 8), Size = new Size(78, 20), Radius = 10, BackColor = on ? Theme.GoldSoft : Theme.Bg2, Edge = on ? Theme.GoldLine : Theme.Line };
        SettingsParts.Text(state, on ? L.T("● ACTIVO", "● ON") : L.T("● INACTIVO", "● OFF"), 7.0f, FontStyle.Bold, on ? Theme.GoldHi : Theme.Danger, new Point(0, 0), new Size(78, 20), ContentAlignment.MiddleCenter);
        card.Controls.Add(state);
        var address = Field(card, new Point(13, 34), RightWidth - 26 - 72, url ?? L.T("Puerto ocupado", "Port in use"));
        var copy = Button(L.T("Copiar", "Copy"), new Point(RightWidth - 13 - 66, 34), new Size(66, 30), 10);
        copy.Enabled = on;
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(url!);
                copy.Text = "✓";
            }
            catch (Exception)
            {
                // the clipboard is busy: leave the address selected to copy by hand
                address.Focus();
                address.SelectAll();
            }
        };
        card.Controls.Add(copy);
        SettingsParts.Text(card, on
                ? L.T("En OBS: fuente «Navegador», fondo transparente.", "In OBS: a \"Browser\" source, transparent background.")
                : L.F("Puerto ocupado por otro programa: reinicia {0}.", "Port taken by another program: restart {0}.", AppInfo.ShortName),
            7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 72), new Size(RightWidth - 26, 16));
    }

    /// <summary>
    /// The backups made when the stats are reset: the latest one, and a button to bring one back into the history
    /// (several: a list to pick from).
    /// </summary>
    private void BackupCard()
    {
        const int ButtonWidth = 92;
        var card = Card(this, RightX, Header + 415, RightWidth, 52, 14);
        Title(card, L.T("Copia de seguridad", "Backup"), ButtonWidth + 8);
        _backupLine = SettingsParts.Text(card, "", 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 29), new Size(RightWidth - 26 - ButtonWidth - 8, 16));
        _restore = Button(L.T("Restaurar…", "Restore…"), new Point(RightWidth - 13 - ButtonWidth, 10), new Size(ButtonWidth, 32), 10);
        _restore.Click += (_, _) => ChooseBackup();
        card.Controls.Add(_restore);
        ShowBackups();
    }

    /// <summary>
    /// The two actions with no way back, together and in red (reset the stats; uninstall), and Close where the main
    /// window has its buttons.
    /// </summary>
    private void BottomButtons(out PillButton close)
    {
        const int UninstallWidth = 104, Y = Header + 476; // under both columns, which end together at 467
        var reset = Button(L.T("Restablecer estadísticas…", "Reset statistics…"), new Point(LeftX, Y), new Size(LeftWidth - UninstallWidth - 6, 40), 12);
        reset.ForeColor = Theme.Danger;
        reset.Click += (_, _) => ResetStats();
        Controls.Add(reset);
        var uninstall = Button(L.T("Desinstalar…", "Uninstall…"), new Point(LeftX + LeftWidth - UninstallWidth, Y), new Size(UninstallWidth, 40), 12);
        uninstall.ForeColor = Theme.Danger;
        uninstall.Click += (_, _) => Uninstall();
        Controls.Add(uninstall);
        close = new PillButton
        {
            Text = L.T("Cerrar", "Close"), Location = new Point(RightX, Y), Size = new Size(RightWidth, 40), Radius = 12,
            BackColor = Theme.Accent, HoverColor = Theme.AccentHover, ForeColor = Theme.AccentInk, DialogResult = DialogResult.OK,
        };
        Controls.Add(close);
    }

    /// <summary>The head of each page section: the solid gold icon tile, a gold title, a line under it; the language on the right.</summary>
    private void HeaderRow(string? language)
    {
        Controls.Add(new IconTile { IconSvg = Icons.Settings, Solid = true, Location = new Point(LeftX, 14), Size = new Size(36, 36) });
        Controls.Add(new FitLabel
        {
            Text = L.T("Configuración", "Settings"), Gold = true, Font = Theme.Display(14f, FontStyle.Bold), BackColor = Color.Transparent,
            Location = new Point(LeftX + 44, 10), Size = new Size(230, 28), UseMnemonic = false,
        });
        SettingsParts.Text(this, L.T("Los cambios se guardan al momento", "Changes are saved right away"), 8.0f, FontStyle.Regular, Theme.Muted, new Point(LeftX + 47, 36), new Size(230, 16));
        Controls.Add(new Panel { BackColor = Theme.Line, Location = new Point(LeftX, 60), Size = new Size(RightX + RightWidth - LeftX, 1) });
        Controls.Add(new LanguagePicker(language, new Point(RightX + RightWidth - 172, 17), LanguageChanged).Button);
    }

    /// <summary>A setting may have changed on the stats page: the switches follow (on the window's thread, without saving).</summary>
    private void OnMonitorChanged(MonitorState _)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }
        try
        {
            BeginInvoke(new Action(ShowSettings));
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // closing
        }
    }

    private void ShowSettings()
    {
        ShowBackups();
        if (!_earlier.Focused && EarlierValue() != Math.Min(MaxEarlierChests, _monitor.InitialChests))
        {
            ShowEarlier(_monitor.InitialChests); // e.g. brought back by restoring a backup
        }
        var config = _monitor.Config;
        _loading = true;
        try
        {
            foreach (var (toggle, on) in new[]
                     {
                         (_openStats, config.OpenStatsOnStart), (_mates, config.ShowTeammates), (_addMates, config.CountTeammateChests),
                         (_ranked, config.CountRanked), (_ammo, config.CountAmmoBoxes), (_food, config.CountFoodBoxes), (_cannons, config.CountLootCannons),
                     })
            {
                if (toggle.Checked != on)
                {
                    toggle.Checked = on;
                }
            }
        }
        finally
        {
            _loading = false;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle);
    }
}
