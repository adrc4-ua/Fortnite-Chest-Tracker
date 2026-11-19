using FCT.Overlay;

namespace FCT.Tests;

/// <summary>Only an OBS browser source hides the overlay over the game; a normal browser does not.</summary>
public sealed class ObsServerTests : IDisposable
{
    private readonly string _url;

    public ObsServerTests()
    {
        ObsServer.Start(7591); // away from the app's ports (7373+, 7473+)
        _url = ObsServer.Url ?? throw new InvalidOperationException("no free port for the test");
    }

    public void Dispose() => ObsServer.Stop();

    private static HttpClient Client(string userAgent)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        return http;
    }

    [Fact]
    public async Task NormalBrowser_DoesNotCountAsObs()
    {
        using var http = Client("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/127.0.0.0 Safari/537.36");
        await http.GetStringAsync(_url);
        await http.GetStringAsync(_url + "view");
        Assert.False(ObsServer.IsObsActive);
    }

    [Fact]
    public async Task OverlayPage_AsksForTheObsViewInsideObs()
    {
        using var http = Client("Mozilla/5.0");
        var page = await http.GetStringAsync(_url);
        Assert.Contains("window.obsstudio ? '/view/obs' : '/view'", page);
    }

    [Fact]
    public async Task ObsView_CountsAsObs_UntilItStopsAsking()
    {
        using var http = Client("Mozilla/5.0");
        var answer = await http.GetAsync(_url + "view/obs");
        Assert.True(answer.IsSuccessStatusCode);
        Assert.True(ObsServer.IsObsActive);

        await Task.Delay(5300); // no request for more than 5 s: removed from OBS (or OBS closed)
        Assert.False(ObsServer.IsObsActive);
    }

    [Fact]
    public async Task OlderPageInObs_IsRecognisedByOBSsUserAgent()
    {
        using var http = Client("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/127.0.0.0 Safari/537.36 OBS/31.0.0");
        await http.GetStringAsync(_url + "view");
        Assert.True(ObsServer.IsObsActive);
    }
}
