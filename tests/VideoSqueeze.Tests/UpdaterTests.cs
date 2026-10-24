using Xunit;

namespace VideoSqueeze.Tests;

/// <summary>Reading GitHub's "latest release" response.</summary>
public class UpdaterTests
{
    static string Release(string tag, string asset = "VideoSqueeze-Setup-1.6.0.exe", string digest = "\"sha256:ab12\"") => $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "https://github.com/TNT24/VideoSqueeze/releases/tag/{{tag}}",
          "assets": [
            { "name": "notes.txt", "size": 10, "browser_download_url": "https://example.invalid/notes.txt", "digest": null },
            { "name": "{{asset}}", "size": 12345, "browser_download_url": "https://example.invalid/setup.exe", "digest": {{digest}} }
          ]
        }
        """;

    [Fact]
    public void NewerRelease_IsOffered_WithItsInstaller()
    {
        var u = Updater.Parse(Release("v1.6.0"), new Version(1, 5, 0));
        Assert.NotNull(u);
        Assert.Equal(new Version(1, 6, 0), u.Version);
        Assert.Equal("https://example.invalid/setup.exe", u.InstallerUrl);
        Assert.Equal(12345, u.Size);
        Assert.Equal("ab12", u.Sha256);
    }

    [Theory]
    [InlineData("v1.5.0")]
    [InlineData("v1.4.9")]
    [InlineData("1.5")] // same version written without the patch number
    [InlineData("nightly")]
    public void SameOlderOrUnreadableRelease_IsIgnored(string tag) =>
        Assert.Null(Updater.Parse(Release(tag), new Version(1, 5, 0)));

    [Fact]
    public void ReleaseWithoutInstaller_IsIgnored() =>
        Assert.Null(Updater.Parse(Release("v1.6.0", asset: "VideoSqueeze-1.6.0.zip"), new Version(1, 5, 0)));

    [Fact]
    public void MissingDigest_StillOffered_WithoutChecksum()
    {
        var u = Updater.Parse(Release("v1.6.0", digest: "null"), new Version(1, 5, 0));
        Assert.NotNull(u);
        Assert.Null(u.Sha256);
    }

    [Fact]
    public void Updater_StartsUp()
    {
        // Parse alone never sets up the HTTP client; 1.5.0/1.5.1 shipped with a client that failed to initialize
        // (empty version in its User-Agent), so every update check failed silently.
        var current = Updater.Current;
        var check = Updater.CheckAsync(new CancellationToken(canceled: true));
        Assert.True(check.IsCanceled || check.IsFaulted && check.Exception!.GetBaseException() is OperationCanceledException,
            check.Exception?.GetBaseException().ToString());
        Assert.NotEqual(new Version(0, 0, 0), current);
    }
}
