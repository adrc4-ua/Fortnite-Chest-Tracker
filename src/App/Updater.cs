using System.Security.Cryptography;
using System.Text.Json;

namespace FCT.App;

/// <summary>A newer release: its version, where to download FCT.exe from and how to check the download.</summary>
public sealed record UpdateInfo(Version Version, string Tag, string AssetUrl, long Size, string? Sha256, string PageUrl);

/// <summary>
/// Self-update from the repository's latest GitHub release. Windows lets a running exe be renamed but not
/// overwritten, so the new exe is downloaded next to it, checked, and swapped in: the running one becomes *.old (deleted
/// at the next start) and the new one takes its name. Can be turned off in Settings.
/// </summary>
public static class Updater
{
    /// <summary>FCT_UPDATE_API points the check at a fake server in the update tests.</summary>
    private static string LatestReleaseUrl =>
        Environment.GetEnvironmentVariable("FCT_UPDATE_API") ?? $"https://api.github.com/repos/{AppInfo.Repo}/releases/latest";

    // ---- Checking -----------------------------------------------------------------------------------------------------------

    /// <summary>The latest release when it is newer than this one and carries FCT.exe; null otherwise or on any failure.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var github = GitHubClient(TimeSpan.FromSeconds(10));
            using var reply = await github.GetAsync(LatestReleaseUrl);
            if (!reply.IsSuccessStatusCode)
            {
                return null;
            }
            using var json = JsonDocument.Parse(await reply.Content.ReadAsStringAsync());
            return ReadRelease(json.RootElement);
        }
        catch
        {
            return null; // offline, GitHub down, an unexpected reply…: no update, never an error for the player
        }
    }

    private static UpdateInfo? ReadRelease(JsonElement release)
    {
        bool Flag(string name) => release.TryGetProperty(name, out var value) && value.GetBoolean();
        if (Flag("draft") || Flag("prerelease"))
        {
            return null;
        }
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var offered) || ThreeParts(offered) <= ThreeParts(AppInfo.Version))
        {
            return null;
        }
        var page = release.TryGetProperty("html_url", out var html) ? html.GetString() ?? AppInfo.RepoUrl : AppInfo.RepoUrl;
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (!string.Equals(asset.GetProperty("name").GetString(), AppInfo.ExeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // GitHub publishes "sha256:<hex>" for every asset; older releases may not have it.
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            var sha256 = digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest["sha256:".Length..] : null;
            return new UpdateInfo(offered, tag, asset.GetProperty("browser_download_url").GetString()!, asset.GetProperty("size").GetInt64(), sha256, page);
        }
        return null;
    }

    /// <summary>Compares only major.minor.patch (a missing patch counts as 0).</summary>
    private static Version ThreeParts(Version v) => new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);

    private static HttpClient GitHubClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.ShortName}/{AppInfo.VersionText}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    // ---- Installing ---------------------------------------------------------------------------------------------------------

    /// <summary>Downloads, checks and swaps in the new exe. Null when it worked, else why it did not (nothing is left half done).</summary>
    public static string? Install(UpdateInfo update)
    {
        if (AppInfo.ExePath is not { } exe)
        {
            return L.F("La actualización solo es posible desde el archivo .exe de {0}", "Updating is only possible from the {0} .exe file", AppInfo.ShortName);
        }
        var incoming = exe + ".download";
        try
        {
            Download(update.AssetUrl, incoming);
            Verify(incoming, update);
            if (OperatingSystem.IsWindows() && !AppInfo.Isolated)
            {
                FortniteWatch.StopStandby(); // an old-style standby may be running this very exe (never the normal copy's from a test one)
            }
            Swap(exe, incoming);
            return null;
        }
        catch (Exception ex)
        {
            TryDelete(incoming);
            return ex.Message;
        }
    }

    private static void Download(string url, string path)
    {
        using var github = GitHubClient(TimeSpan.FromMinutes(5));
        using var body = github.GetStreamAsync(url).GetAwaiter().GetResult();
        using var file = File.Create(path);
        body.CopyTo(file);
    }

    /// <summary>Same size as announced, a Windows executable ("MZ"), and the published SHA-256 when there is one.</summary>
    private static void Verify(string path, UpdateInfo update)
    {
        if (new FileInfo(path).Length != update.Size)
        {
            throw new IOException(L.T("descarga incompleta", "incomplete download"));
        }
        using var file = File.OpenRead(path);
        Span<byte> start = stackalloc byte[2];
        if (file.Read(start) != 2 || start[0] != (byte)'M' || start[1] != (byte)'Z')
        {
            throw new IOException(L.T("el archivo descargado no es un .exe", "the downloaded file is not an .exe"));
        }
        if (update.Sha256 is { } expected)
        {
            file.Position = 0;
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(L.T("suma SHA-256 incorrecta", "wrong SHA-256 checksum"));
            }
        }
    }

    /// <summary>Running exe → *.old, download → exe. If the second step fails, the old exe is put back.</summary>
    private static void Swap(string exe, string incoming)
    {
        var retired = exe + ".old";
        if (File.Exists(retired) && !TryDelete(retired))
        {
            retired = $"{exe}.{DateTime.Now:yyyyMMddHHmmssfff}.old"; // a previous one still locked
        }
        Retry(() => File.Move(exe, retired));
        try
        {
            Retry(() => File.Move(incoming, exe));
        }
        catch
        {
            // e.g. an antivirus holding the new file: never leave the user without an exe
            if (!File.Exists(exe) && File.Exists(retired))
            {
                try { Retry(() => File.Move(retired, exe)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    /// <summary>File moves right after a download can hit a short lock (antivirus scan): a few quick retries.</summary>
    private static void Retry(Action move)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                move();
                return;
            }
            catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(150);
            }
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Removes what the previous update left next to the exe (*.old, an interrupted *.download).</summary>
    public static void CleanupOld()
    {
        if (AppInfo.ExePath is not { } exe || Path.GetDirectoryName(exe) is not { } folder)
        {
            return;
        }
        foreach (var leftover in Directory.EnumerateFiles(folder, Path.GetFileName(exe) + ".*"))
        {
            if (leftover.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || leftover.EndsWith(".download", StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(leftover); // still locked: next time
            }
        }
    }
}
