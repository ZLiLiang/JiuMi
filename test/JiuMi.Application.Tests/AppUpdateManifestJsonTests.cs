using System.Globalization;
using JiuMi.Application.Updates;

namespace JiuMi.Application.Tests;

public sealed class AppUpdateManifestJsonTests
{
    [Fact]
    public void TryParse_GivenBothChannels_ParsesAllFields()
    {
        const string json = """
            {
              "generatedAt": "2026-11-22T08:30:00Z",
              "stable": {
                "version": "1.4.0",
                "prerelease": false,
                "publishedAt": "2026-11-20T10:00:00Z",
                "pageUrl": "https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.0",
                "assetUrl": "https://github.com/ZLiLiang/JiuMi/releases/download/v1.4.0/JiuMi-1.4.0-win-x64.zip",
                "assetSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "assetSize": 74664753
              },
              "preview": {
                "version": "1.5.0-preview.1",
                "prerelease": true,
                "publishedAt": "2026-11-22T08:00:00Z",
                "pageUrl": "https://github.com/ZLiLiang/JiuMi/releases/tag/v1.5.0-preview.1",
                "assetUrl": "https://github.com/ZLiLiang/JiuMi/releases/download/v1.5.0-preview.1/JiuMi-1.5.0-preview.1-win-x64.zip",
                "assetSha256": "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
                "assetSize": 75000000
              },
              "unknownFutureField": true
            }
            """;

        bool ok = AppUpdateManifestJson.TryParse(json, out AppUpdateManifest? manifest, out string? error);

        Assert.True(ok, error);
        Assert.NotNull(manifest);
        Assert.Equal(
            DateTimeOffset.Parse("2026-11-22T08:30:00Z", CultureInfo.InvariantCulture),
            manifest!.GeneratedAt);
        Assert.NotNull(manifest.Stable);
        Assert.Equal(SemanticVersion.Parse("1.4.0"), manifest.Stable!.Version);
        Assert.False(manifest.Stable.IsPrerelease);
        Assert.Equal("https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.0", manifest.Stable.PageUrl.AbsoluteUri);
        Assert.Equal(74664753L, manifest.Stable.AssetSize);
        Assert.NotNull(manifest.Preview);
        Assert.Equal(SemanticVersion.Parse("1.5.0-preview.1"), manifest.Preview!.Version);
        Assert.True(manifest.Preview.IsPrerelease);
    }

    [Fact]
    public void TryParse_GivenNullChannels_SucceedsWithNullEntries()
    {
        const string json = """
            {
              "generatedAt": "2026-11-22T00:00:00Z",
              "stable": null,
              "preview": null
            }
            """;

        bool ok = AppUpdateManifestJson.TryParse(json, out AppUpdateManifest? manifest, out string? error);

        Assert.True(ok, error);
        Assert.NotNull(manifest);
        Assert.Null(manifest!.Stable);
        Assert.Null(manifest.Preview);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"generatedAt": "not-a-date", "stable": null, "preview": null}""")]
    [InlineData("""{"generatedAt": "2026-11-22T00:00:00Z", "stable": {"version": "bad", "prerelease": false, "publishedAt": "2026-11-22T00:00:00Z", "pageUrl": "https://example.test/r"}}""")]
    [InlineData("""{"generatedAt": "2026-11-22T00:00:00Z", "stable": {"version": "1.4.0", "prerelease": false, "publishedAt": "2026-11-22T00:00:00Z", "pageUrl": "http://insecure.example/r"}}""")]
    [InlineData("""{"generatedAt": "2026-11-22T00:00:00Z", "stable": {"version": "1.4.0", "prerelease": false, "publishedAt": "2026-11-22T00:00:00Z", "pageUrl": "file:///C:/Windows/notepad.exe"}}""")]
    public void TryParse_GivenInvalidManifest_ReturnsFalse(string json)
    {
        bool ok = AppUpdateManifestJson.TryParse(json, out AppUpdateManifest? manifest, out string? error);

        Assert.False(ok);
        Assert.Null(manifest);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
