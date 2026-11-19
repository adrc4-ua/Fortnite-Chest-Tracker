using System.Net;
using System.Net.Sockets;

namespace FCT.Overlay;

/// <summary>
/// FCT's web server, on this PC only (loopback) and only while FCT runs:
/// <list type="bullet">
/// <item>"/" (or "/overlay"): the overlay page for OBS, which refreshes its figures from "/view" every 2 s ("/view/obs" inside OBS, which hides the overlay over the game).</item>
/// <item>"/estadisticas": the stats page, so its own settings tab can change settings with "POST /api/ajuste".</item>
/// </list>
/// Safety: a request whose Host is not localhost/127.0.0.1 is refused (a website pointing its own name at 127.0.0.1
/// cannot read anything). Changing a setting also needs the "X-FCT: 1" header (a website could only send it after a
/// CORS preflight, which is never accepted) and, if there is an Origin, this very server.
/// </summary>
public static class ObsServer
{
    public const int DefaultPort = 7373;
    private const int PortsToTry = 10;
    private const int ObsTimeoutMs = 5000;

    private static TcpListener? _listener;
    private static volatile string _overlayBox = "";
    private static long _lastObsPollTicks;

    /// <summary>An OBS browser source is showing the overlay (it asked for it in the last few seconds).</summary>
    public static bool IsObsActive
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastObsPollTicks);
            return ticks > 0 && (Environment.TickCount64 - ticks) < ObsTimeoutMs;
        }
    }

    /// <summary>Address to paste in OBS; null when no port could be opened.</summary>
    public static string? Url { get; private set; }

    /// <summary>Address of the stats page as served here (the only place where its settings tab works).</summary>
    public static string? StatsUrl => Url == null ? null : Url + "estadisticas";

    /// <summary>The stats file served at "/estadisticas".</summary>
    public static string? StatsFile { get; set; }

    /// <summary>Language of the current stats page, reported by "/api/ping" so an open page knows when to reload.</summary>
    public static volatile string? StatsLang;

    /// <summary>The settings the stats page shows (<see cref="FCT.Stats.StatsPage.SettingsMark"/>): a page made with others reloads.</summary>
    public static volatile string? StatsSettings;

    /// <summary>Mark of the current stats page's content: an open page whose mark differs fetches the new one.</summary>
    public static volatile string? StatsStamp;

    /// <summary>Applies a setting sent by the stats page: (key, value) → accepted.</summary>
    public static Func<string, string, bool>? Settings { get; set; }

    /// <summary>Opens the first free port from <paramref name="port"/> on. Calling it again does nothing.</summary>
    public static void Start(int port)
    {
        if (_listener != null)
        {
            return;
        }
        for (var candidate = port; candidate < port + PortsToTry; candidate++)
        {
            var listener = new TcpListener(IPAddress.Loopback, candidate);
            try
            {
                listener.Start();
            }
            catch (SocketException)
            {
                continue; // taken
            }
            _listener = listener;
            Url = $"http://localhost:{candidate}/";
            new Thread(() => Accept(listener, candidate)) { IsBackground = true, Name = "OBS" }.Start();
            return;
        }
        ErrorLog.Write($"OBS: no free port from {port}", new InvalidOperationException("no free port"));
    }

    public static void Stop()
    {
        try
        {
            _listener?.Stop();
        }
        catch (SocketException)
        {
        }
        _listener = null;
        Url = null;
        Interlocked.Exchange(ref _lastObsPollTicks, 0);
        if (OperatingSystem.IsWindows() && NativeOverlay.IsRunning)
        {
            NativeOverlay.SetSuppressedByObs(false);
        }
    }

    /// <summary>New figures for the OBS page.</summary>
    public static void Update(OverlayView view) => _overlayBox = OverlayPage.Box(view);

    private static void Accept(TcpListener listener, int port)
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                return; // Stop() closed the listener
            }
            ThreadPool.QueueUserWorkItem(_ => LocalHttp.Serve(client, request => Route(request, port)));
        }
    }

    private static LocalResponse Route(LocalRequest request, int port)
    {
        if (!IsThisServer(request.Header("Host") ?? "", port, scheme: ""))
        {
            return LocalResponse.Text("403 Forbidden", "forbidden");
        }
        if (request.Method == "GET" && IsObsSource(request))
        {
            RecordObsActivity();
        }
        return (request.Method, request.Path) switch
        {
            ("GET", "/" or "/overlay") => LocalResponse.Html(OverlayPage.LivePage(_overlayBox)),
            ("GET", "/view" or "/view/obs") => LocalResponse.Html(_overlayBox),
            ("GET", "/estadisticas") => StatsFile != null && File.Exists(StatsFile)
                ? LocalResponse.Html(File.ReadAllText(StatsFile))
                : LocalResponse.Text("404 Not Found", "stats page not ready"),
            ("GET", "/api/ping") => LocalResponse.Json($"{{\"ok\":true,\"lang\":\"{StatsLang ?? ""}\",\"settings\":\"{StatsSettings ?? ""}\",\"stamp\":\"{StatsStamp ?? ""}\"}}"),
            ("POST", "/api/ajuste") => ChangeSetting(request, port),
            _ => LocalResponse.Text("404 Not Found", "not found"), // OPTIONS too: no preflight is ever accepted
        };
    }

    /// <summary>
    /// A request of the overlay from inside OBS: the page asks for /view/obs when it runs in a browser source (it has
    /// window.obsstudio). A page OBS loaded from an older version asks for /view: OBS's browser names itself in its
    /// user agent ("OBS/30.2.3"). A normal browser opening the address does not count.
    /// </summary>
    private static bool IsObsSource(LocalRequest request) =>
        request.Path == "/view/obs"
        || (request.Path is "/" or "/overlay" or "/view"
            && request.Headers.TryGetValue("User-Agent", out var agent) && agent.Contains(" OBS/", StringComparison.Ordinal));

    private static void RecordObsActivity()
    {
        var wasActive = IsObsActive;
        Interlocked.Exchange(ref _lastObsPollTicks, Environment.TickCount64);
        if (!wasActive && OperatingSystem.IsWindows() && NativeOverlay.IsRunning)
        {
            NativeOverlay.SetSuppressedByObs(true);
        }
    }

    /// <summary>"localhost:port" (any case) or "127.0.0.1:port", optionally with a scheme in front (for Origin).</summary>
    private static bool IsThisServer(string value, int port, string scheme) =>
        value.Equals($"{scheme}localhost:{port}", StringComparison.OrdinalIgnoreCase) || value.Equals($"{scheme}127.0.0.1:{port}", StringComparison.Ordinal);

    /// <summary>Body "key=value" (form-encoded), accepted only from the stats page itself.</summary>
    private static LocalResponse ChangeSetting(LocalRequest request, int port)
    {
        var origin = request.Header("Origin");
        var trusted = request.Header("X-FCT") == "1" && (origin == null || IsThisServer(origin, port, "http://"));
        if (!trusted || Settings is not { } apply)
        {
            return LocalResponse.Json("{\"ok\":false}", "403 Forbidden");
        }
        var separator = request.Body.IndexOf('=');
        var key = WebUtility.UrlDecode(separator < 0 ? request.Body : request.Body[..separator]);
        var value = separator < 0 ? "" : WebUtility.UrlDecode(request.Body[(separator + 1)..]);
        return apply(key, value) ? LocalResponse.Json("{\"ok\":true}") : LocalResponse.Json("{\"ok\":false}", "400 Bad Request");
    }
}
