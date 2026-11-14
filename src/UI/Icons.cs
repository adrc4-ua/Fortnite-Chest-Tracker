using FCT.Stats;

namespace FCT.UI;

/// <summary>
/// Iconos de la app: los de la página de estadísticas (mismo dibujo) y unos pocos propios con el mismo trazo
/// (24 × 24, línea de 1,7 redondeada, relleno suave con class="f").
/// </summary>
internal static class Icons
{
    private const string Svg = """<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">""";

    public static string Chest => PageIcons.Chest;
    public static string Summary => PageIcons.Tabs["resumen"];
    public static string Treasure => PageIcons.Gem;
    public static string Settings => PageIcons.Tabs["ajustes"];
    public static string Teammates => PageIcons.Tabs["companeros"];
    public static string Matches => PageIcons.Figures["matches"];
    public static string Average => PageIcons.Figures["avg"];
    public static string Record => PageIcons.Figures["best"];
    public static string Day => PageIcons.Figures["day"];
    public static string Wins => PageIcons.Figures["wins"];

    /// <summary>Exit: a power button.</summary>
    public const string Power = Svg + """<path d="M12 3.5v8"/><path d="M7.4 6.6a7.5 7.5 0 1 0 9.2 0"/></svg>""";

    /// <summary>Overlay: una pantalla con el recuadro del contador en una esquina.</summary>
    public const string Overlay = Svg + """<rect class="f" x="3" y="4.5" width="18" height="12" rx="2.5"/><path d="M8.5 20h7M12 16.5V20"/><rect x="12.5" y="7" width="5.5" height="3.6" rx="1" fill="currentColor" stroke="none"/></svg>""";

    /// <summary>Perfil (cuenta de Epic).</summary>
    public const string Person = Svg + """<circle class="f" cx="12" cy="8" r="3.6"/><path class="f" d="M5 20a7 7 0 0 1 14 0z"/></svg>""";

    /// <summary>Hoy: un rayo de actividad.</summary>
    public const string Today = Svg + """<path class="f" d="M13.5 2.5 4.5 14h7l-1 7.5 9-11.5h-7z"/></svg>""";

    /// <summary>Gráfico semanal.</summary>
    public const string Week = Svg + """<rect class="f" x="4" y="12" width="4" height="8" rx="1"/><rect class="f" x="10" y="5" width="4" height="15" rx="1"/><rect class="f" x="16" y="9" width="4" height="11" rx="1"/></svg>""";

    /// <summary>Aviso o información.</summary>
    public const string Info = Svg + """<circle class="f" cx="12" cy="12" r="9"/><path d="M12 11v5.5M12 7.6v.1"/></svg>""";

    /// <summary>Descarga (actualización).</summary>
    public const string Download = Svg + """<path d="M12 3.5v11M7.5 10.5 12 15l4.5-4.5"/><path class="f" d="M4 15.5v2A2.5 2.5 0 0 0 6.5 20h11a2.5 2.5 0 0 0 2.5-2.5v-2"/></svg>""";

    /// <summary>Bienvenida: destellos.</summary>
    public const string Sparkle = Svg + """<path class="f" d="M12 3.5 13.9 9.1 19.5 11 13.9 12.9 12 18.5 10.1 12.9 4.5 11 10.1 9.1z"/><path d="M18.5 3.5v3M17 5h3M5.5 17.5v3M4 19h3"/></svg>""";

    /// <summary>Grabar (las repeticiones de Fortnite): una cámara con el punto de grabación.</summary>
    public const string RecordReplays = Svg + """<rect class="f" x="3" y="6.5" width="13" height="11" rx="2.5"/><path d="m16 10.5 5-3v9l-5-3"/><circle cx="7.5" cy="12" r="1.6" fill="currentColor" stroke="none"/></svg>""";

    /// <summary>Partidas guardadas: una carpeta.</summary>
    public const string Folder = Svg + """<path class="f" d="M3.5 7.5A2 2 0 0 1 5.5 5.5h4l2 2.5h7a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z"/><path d="M3.5 10.5h17"/></svg>""";

    /// <summary>Buscar.</summary>
    public const string Search = Svg + """<circle class="f" cx="10.5" cy="10.5" r="6.5"/><path d="m15.5 15.5 5 5"/></svg>""";

    /// <summary>Hecho, encontrado.</summary>
    public const string Check = Svg + """<circle class="f" cx="12" cy="12" r="9"/><path d="m8 12.3 2.7 2.7L16.2 9.5"/></svg>""";

    /// <summary>Atención (no se pudo, falta algo).</summary>
    public const string Warning = Svg + """<path class="f" d="M12 3.8 21 19.5H3z"/><path d="M12 10v4.5M12 17.2v.1"/></svg>""";

    /// <summary>Idioma.</summary>
    public const string Globe = Svg + """<circle class="f" cx="12" cy="12" r="8.5"/><path d="M3.5 12h17M12 3.5c2.6 2.4 3.8 5.2 3.8 8.5s-1.2 6.1-3.8 8.5c-2.6-2.4-3.8-5.2-3.8-8.5S9.4 5.9 12 3.5z"/></svg>""";

    /// <summary>Cálculo (cofres de antes).</summary>
    public const string Calc = Svg + """<rect class="f" x="5" y="3" width="14" height="18" rx="2.5"/><path d="M8.5 7h7M8.5 11.5h.1M12 11.5h.1M15.5 11.5h.1M8.5 15h.1M12 15h.1M15.5 15h.1M8.5 18h7"/></svg>""";

    /// <summary>Abrir fuera (página en el navegador).</summary>
    public const string External = Svg + """<path d="M14 4h6v6M20 4l-8.5 8.5M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4"/></svg>""";
}
