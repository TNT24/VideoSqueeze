using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace VideoSqueeze;

public record Update(Version Version, string InstallerUrl, long Size, string? Sha256, string PageUrl);

/// <summary>Finds, downloads and installs newer releases published on GitHub.</summary>
static class Updater
{
    const string LatestRelease = "https://api.github.com/repos/TNT24/VideoSqueeze/releases/latest";

    // Must stay above `http`: static fields are initialized top to bottom, and CreateClient reads Current.
    public static Version Current { get; } = Normalize(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0));
    static readonly HttpClient http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"VideoSqueeze/{Current}"); // GitHub rejects requests without one
        return c;
    }

    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>The newest release, or null if this is already the latest version.</summary>
    public static async Task<Update?> CheckAsync(CancellationToken ct) => Parse(await http.GetStringAsync(LatestRelease, ct), Current);

    /// <summary>The release's installer, if the release is newer than <paramref name="current"/>.</summary>
    internal static Update? Parse(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v) || Normalize(v) <= current) return null;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith("VideoSqueeze-Setup", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            // GitHub publishes a "sha256:…" digest for each upload; used to reject damaged downloads.
            string? sha = a.TryGetProperty("digest", out var d) && d.GetString() is { } s && s.StartsWith("sha256:") ? s[7..] : null;
            return new Update(Normalize(v), a.GetProperty("browser_download_url").GetString()!, a.GetProperty("size").GetInt64(), sha,
                root.TryGetProperty("html_url", out var page) ? page.GetString() ?? "" : "");
        }
        return null;
    }

    public static async Task<string> DownloadAsync(Update u, Action<double> progress, CancellationToken ct)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VideoSqueeze", "update")).FullName;
        var path = Path.Combine(dir, $"VideoSqueeze-Setup-{u.Version}.exe");
        var part = path + ".part";
        using (var resp = await http.GetAsync(u.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(part);
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (u.Size > 0) progress(Math.Min(1, (double)done / u.Size));
            }
        }
        if (u.Sha256 != null)
        {
            string hash;
            await using (var f = File.OpenRead(part)) hash = Convert.ToHexString(await SHA256.HashDataAsync(f, ct));
            if (!hash.Equals(u.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(part);
                throw new Exception("The download was damaged (checksum mismatch). Try again later.");
            }
        }
        File.Move(part, path, true);
        return path;
    }

    /// <summary>Starts the installer silently. The app must exit right after; the installer starts it again when done.</summary>
    public static void Install(string installer)
    {
        // Update the existing install in place: an all-users install lives under Program Files, a per-user one under AppData.
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        bool allUsers = Environment.ProcessPath?.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) == true;
        Process.Start(new ProcessStartInfo(installer,
            $"/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS {(allUsers ? "/ALLUSERS" : "/CURRENTUSER")}") { UseShellExecute = true });
    }
}
