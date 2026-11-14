using System.Globalization;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>What each control of the settings window does.</summary>
public sealed partial class SettingsForm
{
    /// <summary>Saves a change made by the player (not one the window itself is making).</summary>
    private void Saved(Action save)
    {
        if (!_loading)
        {
            save();
        }
    }

    /// <summary>Flips a switch back without saving anything (its dialog was cancelled, or the change failed).</summary>
    private void Revert(ToggleSwitch toggle)
    {
        _loading = true;
        toggle.Checked = !toggle.Checked;
        _loading = false;
    }

    private void WithFortniteChanged()
    {
        if (_loading || _monitor.SetStartWithFortnite(_withFortnite.Checked))
        {
            return;
        }
        Revert(_withFortnite);
        InfoDialog.Confirm(this, L.T("Desde aquí no se puede", "Not from here"),
            L.F("Para esto, abre {0} desde su archivo .exe.", "For this, open {0} from its .exe file.", AppInfo.ShortName),
            Array.Empty<InfoPoint>(), L.T("Vale", "OK"), null, Icons.Info);
    }

    private void TeammatesChanged(ToggleSwitch toggle)
    {
        if (_loading)
        {
            return;
        }
        if (toggle.Checked && !InfoDialog.Confirm(this, L.T("Compañeros en la ventana", "Teammates in the window"),
                L.T("Verás a tu equipo y los cofres que abre cada uno.", "You'll see your team and the chests each one opens."),
                new[] { TeammatesExact(), TeammatesApproximate() },
                L.T("Activar", "Turn on"), L.T("Cancelar", "Cancel"), Icons.Teammates))
        {
            Revert(toggle);
            return;
        }
        _monitor.SetShowTeammates(toggle.Checked);
    }

    private void AddTeammatesChanged(ToggleSwitch toggle)
    {
        if (_loading)
        {
            return;
        }
        if (toggle.Checked && !InfoDialog.Confirm(this, L.T("Sumar sus cofres a tu total", "Add their chests to your total"),
                L.T("Tu total, tu media, tus récords y el overlay contarán también los cofres de tu equipo.",
                    "Your total, average, records and overlay will also count your team's chests."),
                new[]
                {
                    TeammatesApproximate(),
                    new InfoPoint(Icons.Folder, Theme.GoldHi, L.T("No pierdes nada", "You don't lose anything"),
                        L.T("Si lo quitas, vuelves a ver solo los tuyos.", "Turn it off and you'll see only yours again.")),
                },
                L.T("Activar", "Turn on"), L.T("Cancelar", "Cancel"), Icons.Teammates))
        {
            Revert(toggle);
            return;
        }
        _monitor.SetCountTeammates(toggle.Checked);
    }

    /// <summary>What is exact about teammates.</summary>
    private static InfoPoint TeammatesExact() => new(Icons.Check, Theme.GoldHi, L.T("Esto es exacto", "This is exact"),
        L.T("Con quién juegas, partidas juntos y victorias.", "Who you play with, matches together and wins."));

    /// <summary>What is not: their chests, limited by the distance measured on real replays.</summary>
    private static InfoPoint TeammatesApproximate() => new(Icons.Warning, Theme.GoldHi, L.T("Sus cofres son aproximados", "Their chests are approximate"),
        L.Big("teammatesRange",
            "Fortnite solo graba lo que pasa cerca de ti (a menos de 1 km). Si un compañero se aleja, no vemos sus cofres: lo que sale es un mínimo.",
            "Fortnite only records what happens near you (within about 1 km). If a teammate wanders off, we can't see their chests: what you see is a minimum."));

    /// <summary>Lights the chosen update mode's button and shows its line; saves it when the player picked it.</summary>
    private void ShowUpdateMode(int index, bool save)
    {
        for (var i = 0; i < _updateButtons.Length; i++)
        {
            var on = i == index;
            _updateButtons[i].BackColor = on ? Theme.Accent : Theme.Panel;
            _updateButtons[i].HoverColor = on ? Theme.AccentHover : Theme.PanelHover;
            _updateButtons[i].ForeColor = on ? Theme.AccentInk : Theme.Ink;
            _updateButtons[i].Invalidate();
        }
        _updateHint.Text = UpdateChoices[index].Hint;
        if (save)
        {
            var mode = UpdateChoices[index].Mode;
            _monitor.Change(c => c.UpdateMode = mode);
        }
    }

    /// <summary>The language is saved now and applies when FCT restarts, which is offered right away.</summary>
    private void LanguageChanged(string? code)
    {
        _monitor.SetLanguage(code);
        if (InfoDialog.Confirm(this, L.T("Idioma", "Language"),
                L.F("El idioma cambia al reiniciar {0}. No pierdes nada.", "The language changes when {0} restarts. You don't lose anything.", AppInfo.ShortName),
                Array.Empty<InfoPoint>(), L.T("Reiniciar ahora", "Restart now"), L.T("Más tarde", "Later"), Icons.Globe))
        {
            Close();
            _monitor.Restart();
        }
    }

    // ---- chests from before FCT ----------------------------------------------------------------------------------------

    private int EarlierValue() =>
        int.TryParse(_earlier.Text, NumberStyles.None, L.Culture, out var chests) ? Math.Clamp(chests, 0, MaxEarlierChests) : 0;

    /// <summary>− and +: the text changes, and its change saves the value.</summary>
    private void StepEarlier(int step) => _earlier.Text = Math.Clamp(EarlierValue() + step, 0, MaxEarlierChests).ToString(L.Culture);

    private void ShowEarlier(int chests)
    {
        _loading = true;
        _earlier.Text = Math.Min(MaxEarlierChests, chests).ToString(L.Culture);
        _loading = false;
    }

    /// <summary>
    /// The first start's calculation again: saved matches count the same as then (in the average, and taken off the
    /// season). With none saved yet, its first choice is "start from zero" rather than "only the saved ones".
    /// </summary>
    private void Recalculate()
    {
        var noneSaved = _monitor.PreviewEstimate(1, HistoryEstimator.DefaultSample, null).Replays == 0;
        if (EstimateDialog.Ask(this, _monitor.ShownProfileName, _monitor.ShownAccountId, _monitor.Estimate, _monitor.PreviewEstimate, noneSaved) is not { } choice)
        {
            return;
        }
        _monitor.SetEstimate(choice.Estimate ? choice.TotalMatches : null, choice.Source, choice.Sample, choice.ManualAverage);
        int? shown = !choice.Estimate ? 0 : _monitor.PreviewEstimate(choice.TotalMatches, choice.Sample, choice.ManualAverage).Chests;
        if (shown is { } chests)
        {
            ShowEarlier(chests);
        }
    }

    // ---- folder, reset, uninstall --------------------------------------------------------------------------------------

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = L.T("Carpeta de replays de Fortnite", "Fortnite replays folder"),
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _monitor.ChangeDemosFolder(dialog.SelectedPath);
            _folder.Text = dialog.SelectedPath;
        }
    }

    // ---- Backups ----------------------------------------------------------------------------------------------------------

    /// <summary>The latest backup ("Latest: 14/11/26 18:02 · 78 matches"), read again only when there is a newer one.</summary>
    private void ShowBackups()
    {
        var latest = _store.Backups().FirstOrDefault();
        if (latest.Path == _backupShown && _backupShown != null)
        {
            return;
        }
        _backupShown = latest.Path;
        _restore.Enabled = latest.Path != null;
        _backupLine.Text = latest.Path == null
            ? L.T("Aún no hay: se hace al restablecer", "None yet: made when you reset")
            : $"{L.T("Última", "Latest")}: {latest.Made.ToString("dd/MM/yy HH:mm", L.Culture)} · {MatchesIn(latest.Path)}";
    }

    /// <summary>"78 matches" in a backup ("?" when it cannot be read).</summary>
    private static string MatchesIn(string backup) =>
        JsonFile.Read<TrackerData>(backup) is { } data ? L.Count(data.Matches.Count, "partida", "partidas", "match", "matches") : "?";

    /// <summary>One backup: straight to the question. Several: a list under the button, newest first.</summary>
    private void ChooseBackup()
    {
        var backups = _store.Backups();
        if (backups.Count == 1)
        {
            RestoreBackup(backups[0].Path, backups[0].Made);
            return;
        }
        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, Font = Theme.Text(9f) };
        foreach (var (path, made) in backups)
        {
            var item = new ToolStripMenuItem($"{made.ToString("dd/MM/yy HH:mm", L.Culture)} · {MatchesIn(path)}", null, (_, _) => RestoreBackup(path, made)) { ForeColor = Theme.Ink };
            menu.Items.Add(item);
        }
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(_restore, new Point(0, _restore.Height));
    }

    /// <summary>After confirming, brings the backup's matches (and its chests from before FCT) back into the history.</summary>
    private void RestoreBackup(string path, DateTime made)
    {
        var points = new List<InfoPoint>
        {
            new(Icons.Matches, Theme.GoldHi, L.T("Se suman a tu historial", "They're added to your history"),
                L.T("Vuelven sus partidas, cofres y tesoros. Las que ya tienes, también las que jugaste después, se quedan.",
                    "Its matches, chests and treasures come back. The ones you already have, also those played since, stay.")),
        };
        if (File.Exists(Store.EarlierChestsOf(path)))
        {
            points.Add(new InfoPoint(Icons.Calc, Theme.GoldHi, L.F("Y los cofres de antes de {0}", "And the chests from before {0}", AppInfo.ShortName),
                L.T("Los que tenías al restablecer, si ahora no tienes otros.", "The ones you had when you reset, unless you have others now.")));
        }
        if (!InfoDialog.Confirm(this, L.T("¿Restaurar la copia?", "Restore the backup?"),
                L.F("Copia del {0}: {1}.", "Backup of {0}: {1}.", made.ToString("dd/MM/yy HH:mm", L.Culture), MatchesIn(path)),
                points.ToArray(), L.T("Restaurar", "Restore"), L.T("Cancelar", "Cancel"), Icons.Folder))
        {
            return;
        }
        _monitor.RestoreBackup(path); // the window's strip says how many came back; the figure below follows
    }

    /// <summary>After confirming, deletes the whole history (a copy of data.json is kept first).</summary>
    private void ResetStats()
    {
        if (!InfoDialog.Confirm(this, L.T("¿Empezar de cero?", "Start from zero?"),
                L.T("Se borran tus partidas, cofres y tesoros, y también los cofres de antes.", "Your matches, chests and treasures are deleted, and the earlier chests too."),
                new[]
                {
                    new InfoPoint(Icons.Matches, Theme.GoldHi, L.T("Desde tu próxima partida", "From your next match"),
                        L.T("Las partidas que ya tienes guardadas no se vuelven a contar.", "The matches you already have saved aren't counted again.")),
                    new InfoPoint(Icons.Folder, Theme.GoldHi, L.T("Guardamos una copia", "We keep a copy"),
                        L.T("Por si acaso: la recuperas cuando quieras en Copia de seguridad → Restaurar.", "Just in case: bring it back whenever you want in Backup → Restore.")),
                },
                L.T("Borrar todo", "Delete everything"), L.T("Cancelar", "Cancel"), Icons.Warning))
        {
            return;
        }
        var backup = _monitor.ResetStats();
        ShowEarlier(0);
        _backupShown = null; // the new backup shows once it is written
        InfoDialog.Confirm(this, L.T("Listo, empezamos de cero", "Done, starting from zero"),
            L.T("Si cambias de idea, recupéralo todo en Copia de seguridad → Restaurar.", "If you change your mind, bring it all back in Backup → Restore."),
            Array.Empty<InfoPoint>(), L.T("Vale", "OK"), null, Icons.Check);
    }

    /// <summary>After confirming, closes FCT and removes it from Windows (task, startup entries, the exe). The data stays.</summary>
    private void Uninstall()
    {
        if (!InfoDialog.Confirm(this, L.F("¿Desinstalar {0}?", "Uninstall {0}?", AppInfo.ShortName),
                L.T("Se cierra y no deja nada en Windows.", "It closes and leaves nothing behind in Windows."),
                new[]
                {
                    new InfoPoint(Icons.Check, Theme.GoldHi, L.T("Se borra todo", "Everything is removed"),
                        L.T("El programa y su apertura con Fortnite (la tarea programada y las entradas de inicio).",
                            "The program and its opening with Fortnite (the scheduled task and the startup entries).")),
                    new InfoPoint(Icons.Folder, Theme.GoldHi, L.T("Tus datos se quedan", "Your data stays"),
                        L.F("Tu historial sigue en {0}. Bórralo a mano si quieres; si vuelves a instalar FCT, lo recupera.",
                            "Your history stays in {0}. Delete it by hand if you want; if you install FCT again, it's picked up.", _monitor.DataFolder)),
                },
                L.T("Desinstalar", "Uninstall"), L.T("Cancelar", "Cancel"), Icons.Warning))
        {
            return;
        }
        Close();
        _monitor.Uninstall();
    }
}
