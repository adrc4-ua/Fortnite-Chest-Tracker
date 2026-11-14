using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>
/// The "Configuración" tab: the app's own settings, so a change here is a change there too, and the other way round
/// (the page reloads by itself when the app changes one). Controls work when FCT serves the page (http://localhost);
/// opened as a file they show but are disabled. The app's version is shown here and only here.
/// </summary>
internal static class SettingsTab
{
    public static string Build(Config config)
    {
        var html = new StringBuilder(Head("ajustes", L.T("Configuración", "Settings"),
            L.T("Los mismos ajustes que en la app: lo que cambies aquí cambia también allí", "The same settings as in the app: what you change here changes there too")));
        html.Append($"<p class=\"file-note\" hidden>{FileNote()}</p>").Append(About());

        var languageLabel = L.T("Idioma de la interfaz", "Interface language");
        var select = $"<select class=\"select\" data-key=\"lang\" aria-label=\"{Esc(languageLabel)}\">{LanguageOptions(config.Language)}</select>";
        html.Append("<div class=\"settings\">")
            .Append(Group(L.T("Idioma", "Language"),
                Setting("lang", languageLabel, L.T("Se aplica también en la app (se reinicia sola)", "Also applied to the app (restarts automatically)"), select, column: true)))
            .Append(Group(L.T("Compañeros", "Teammates"),
                Toggle("mates", "teammates", config.ShowTeammates, L.T("Mostrar compañeros", "Show teammates"), L.T("Con quién juegas y sus cofres (hasta 1.087 m de ti)", "Who you play with and their chests (up to 1,087 m from you)"))
                + Toggle("sum", "countMates", config.CountTeammateChests, L.T("Sumar sus cofres a tu total", "Add their chests to your total"), L.T("Tu historial no cambia", "Your history does not change"))))
            .Append(Group(L.T("Contar también", "Also count"),
                Toggle("ranked", "ranked", config.CountRanked, L.T("Partidas clasificatorias", "Ranked matches"),
                    L.T("Battle Royale y Cero construcción", "Battle Royale and Zero Build"))
                + Toggle("ammo", "ammo", config.CountAmmoBoxes, L.T("Cajas de munición", "Ammo boxes"))
                + Toggle("food", "food", config.CountFoodBoxes, L.T("Cajas de comida", "Food boxes"))
                + Toggle("cannons", "cannons", config.CountLootCannons, L.T("Cañones de botín", "Loot cannons"))))
            .Append("</div>");
        return html.ToString();
    }

    /// <summary>Shown (by the script) when the page is a file: where to open it to change anything.</summary>
    private static string FileNote() =>
        ObsServer.StatsUrl is { } url
            ? L.T("Abre las estadísticas con el botón Estadísticas de FCT para cambiar la configuración aquí.", "Open stats with the FCT Stats button to change the settings here.") + $" <a href=\"{url}\">{L.T("Abrir", "Open")} →</a>"
            : L.T("Abre FCT para cambiar la configuración aquí.", "Open FCT to change the settings here.");

    private static string About()
    {
        static string Link(string href, string text) => $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener\">{text} →</a>";
        var logo = AppLogo.DataUri is { } uri ? $"<img src=\"{uri}\" alt=\"\" width=\"76\" height=\"76\">" : "";
        return $"<div class=\"sheet about\">{logo}<div class=\"about-text\"><b>{AppInfo.Name}</b><span class=\"about-ver\">{L.F("Versión {0}", "Version {0}", AppInfo.VersionText)}</span>"
               + $"<small>{L.T("Cuenta tus cofres de Fortnite a partir de tus replays.", "Counts your Fortnite chests from your replays.")}</small></div>"
               + "<div class=\"about-links\">"
               + Link(AppInfo.RepoUrl, "GitHub")
               + Link(AppInfo.RepoUrl + "/releases", L.T("Novedades", "What's new"))
               + Link(AppInfo.IssuesUrl, L.T("Problema o sugerencia", "Problem or suggestion"))
               + Link(AppInfo.RepoUrl + "/blob/main/NOTICE.md", $"GPL-3.0 · {L.T("sin garantía", "no warranty")}")
               + "</div></div>";
    }

    /// <summary>"System (…)" first, then every language; the chosen one selected (a code no longer offered means System).</summary>
    private static string LanguageOptions(string? chosen)
    {
        if (chosen != null && L.Languages.All(l => l.Code != chosen))
        {
            chosen = null;
        }
        var system = L.Languages.FirstOrDefault(l => l.Code == L.SystemCode).Name ?? "English";
        var options = new StringBuilder($"<option value=\"\"{(chosen == null ? " selected" : "")}>{Esc(L.F("Sistema ({0})", "System ({0})", system))}</option>");
        foreach (var (code, name) in L.Languages)
        {
            options.Append($"<option value=\"{code}\"{(chosen == code ? " selected" : "")}>{Esc(name)}</option>");
        }
        return options.ToString();
    }

    private static string Group(string title, string settings) => Section(title, null, $"<div class=\"sheet set-sheet\">{settings}</div>");

    private static string Setting(string icon, string label, string? hint, string control, bool column = false) =>
        $"<div class=\"setting{(column ? " setting-col" : "")}\"><span class=\"set-ico\">{PageIcons.Settings[icon]}</span>"
        + $"<div class=\"setting-txt\"><b>{label}</b>{(hint == null ? "" : $"<small>{hint}</small>")}</div>{control}</div>";

    private static string Toggle(string icon, string key, bool on, string label, string? hint = null) =>
        Setting(icon, label, hint, $"<label class=\"switch\"><input type=\"checkbox\" role=\"switch\" data-key=\"{key}\"{(on ? " checked" : "")} aria-label=\"{Esc(label)}\"><span></span></label>");
}
