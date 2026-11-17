using System.Text;
using static FCT.Stats.PageParts;

namespace FCT.Stats;

/// <summary>
/// The overlay card for OBS: overlay.html (a file that reloads itself) and the live page FCT serves, which fetches the
/// card every two seconds and swaps it only when it changed, so it never flickers.
/// </summary>
public static class OverlayPage
{
    /// <summary>The card alone (also what the live page fetches).</summary>
    public static string Box(OverlayView view)
    {
        var cells = new StringBuilder();
        foreach (var row in view.Rows)
        {
            // treasures: the app's gold gem instead of the emoji
            var value = row.Treasure ? PageIcons.Gem + Esc(row.Value.Replace(OverlayModel.TreasureIcon, "").Trim()) : Esc(row.Value);
            cells.Append($"<div class=\"ov-cell{(row.Treasure ? " ov-gems" : "")}\"><span class=\"ov-cap\">{Esc(row.Label)}</span><b>{value}</b></div>");
        }
        var match = view.Current == null ? " ov-none" : "";
        var est = view.Estimation != null
            ? $"<span class=\"ov-est\">{Esc(L.F("EST. {0}", "EST. {0}", view.Estimation))}</span>"
            : "";
        return "<div class=\"ov\">"
               + $"<div class=\"ov-top\"><span class=\"ov-title\">{L.T("COFRES", "CHESTS")}</span>{est}</div>"
               + "<div class=\"ov-numbers\">"
               + $"<div class=\"ov-total\"><span class=\"ov-big\">{Esc(view.Total)}</span><span class=\"ov-cap\">total</span></div>"
               + $"<div class=\"ov-match{match}\"><span class=\"ov-big\">{Esc(view.Current ?? "—")}</span><span class=\"ov-cap\">{Esc(view.CurrentCaption)}</span></div>"
               + $"</div><div class=\"ov-rows\">{cells}</div></div>";
    }

    /// <summary>overlay.html: reloads itself every 10 s.</summary>
    public static string File(OverlayView view) =>
        Document("<meta http-equiv=\"refresh\" content=\"10\">", Box(view));

    /// <summary>The page FCT serves to OBS.</summary>
    public static string LivePage(string box) =>
        Document("", $"<div id=\"o\">{box}</div><script>{LiveScript}</script>");

    private static string Document(string extraHead, string body) =>
        $"<!doctype html><html lang=\"{L.Code}\"><head><meta charset=\"utf-8\">{extraHead}\n"
        + $"<title>{L.T("Overlay de cofres", "Chest overlay")}</title><style>{OverlayCss}</style></head>\n<body>{body}</body></html>";

    private const string LiveScript = """
        const host = document.getElementById('o');
        let shown = host.innerHTML;
        setInterval(async () => {
          try {
            const answer = await fetch('/view', { cache: 'no-store' });
            if (!answer.ok) return;
            const card = await answer.text();
            if (card !== shown) { host.innerHTML = card; shown = card; }
          } catch { /* FCT closed: keep the last figures */ }
        }, 2000);
        """;
}
