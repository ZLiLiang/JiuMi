using JiuMi.Application.Updates;

namespace JiuMi.Application.Tests;

public sealed class AppUpdateSelectorTests
{
    private static readonly AppUpdateSource _gitee = new(
        "Gitee",
        "https://gitee.com/ZLiLiang/JiuMi",
        new Uri("https://gitee.com/ZLiLiang/JiuMi/releases/download/app-update/update.json"));

    private static readonly AppUpdateSource _github = new(
        "GitHub",
        "https://github.com/ZLiLiang/JiuMi",
        new Uri("https://github.com/ZLiLiang/JiuMi/releases/download/app-update/update.json"));

    private static readonly DateTimeOffset _now = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelectBest_GivenStableInstalled_IgnoresPreviewEvenWhenNewer()
    {
        AppUpdateManifest manifest = new(
            _now,
            Stable: Release("1.4.0", isPrerelease: false, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.0"),
            Preview: Release("1.5.0-preview.1", isPrerelease: true, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.5.0-preview.1"));

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.4.0"),
            [new AppUpdateSourceManifest(_gitee, manifest)]);

        Assert.Null(candidate);
    }

    [Fact]
    public void SelectBest_GivenStableInstalledAndNewerStable_ReturnsNewerStable()
    {
        AppUpdateManifest manifest = new(
            _now,
            Stable: Release("1.4.1", isPrerelease: false, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.1"),
            Preview: Release("1.5.0-preview.1", isPrerelease: true, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.5.0-preview.1"));

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.4.0"),
            [new AppUpdateSourceManifest(_gitee, manifest)]);

        Assert.NotNull(candidate);
        Assert.Equal(SemanticVersion.Parse("1.4.1"), candidate!.Release.Version);
        Assert.False(candidate.Release.IsPrerelease);
    }

    [Fact]
    public void SelectBest_GivenPreviewInstalled_UpgradesToNewerStable()
    {
        AppUpdateManifest manifest = new(
            _now,
            Stable: Release("1.4.0", isPrerelease: false, "https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.0"),
            Preview: Release("1.4.0-preview.1", isPrerelease: true, "https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.0-preview.1"));

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.4.0-preview.1"),
            [new AppUpdateSourceManifest(_github, manifest)]);

        Assert.NotNull(candidate);
        Assert.Equal(SemanticVersion.Parse("1.4.0"), candidate!.Release.Version);
        Assert.False(candidate.Release.IsPrerelease);
    }

    [Fact]
    public void SelectBest_GivenPreviewInstalled_PicksNewerOfStableAndPreview()
    {
        AppUpdateManifest manifest = new(
            _now,
            Stable: Release("1.4.0", isPrerelease: false, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.0"),
            Preview: Release("1.5.0-preview.10", isPrerelease: true, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.5.0-preview.10"));

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.5.0-preview.9"),
            [new AppUpdateSourceManifest(_gitee, manifest)]);

        Assert.NotNull(candidate);
        Assert.Equal(SemanticVersion.Parse("1.5.0-preview.10"), candidate!.Release.Version);
        Assert.True(candidate.Release.IsPrerelease);
    }

    [Fact]
    public void SelectBest_GivenOlderRemoteVersion_NeverDowngrades()
    {
        AppUpdateManifest manifest = new(
            _now,
            Stable: Release("1.3.0", isPrerelease: false, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.3.0"),
            Preview: Release("1.4.0-preview.1", isPrerelease: true, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.0-preview.1"));

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.4.1"),
            [new AppUpdateSourceManifest(_gitee, manifest)]);

        Assert.Null(candidate);
    }

    [Fact]
    public void SelectBest_GivenTwoSourcesWithDifferentVersions_PicksHigherVersionSource()
    {
        AppUpdateManifest giteeManifest = new(
            _now,
            Stable: Release("1.4.1", isPrerelease: false, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.1"),
            Preview: null);
        AppUpdateManifest githubManifest = new(
            _now,
            Stable: Release("1.4.2", isPrerelease: false, "https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.2"),
            Preview: null);

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.4.0"),
            [
                new AppUpdateSourceManifest(_gitee, giteeManifest),
                new AppUpdateSourceManifest(_github, githubManifest),
            ]);

        Assert.NotNull(candidate);
        Assert.Equal(SemanticVersion.Parse("1.4.2"), candidate!.Release.Version);
        Assert.Equal(_github, candidate.Source);
        Assert.Equal("https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.2", candidate.Release.PageUrl.AbsoluteUri);
    }

    [Fact]
    public void SelectBest_GivenTwoSourcesWithSameVersion_PrefersFirstSource()
    {
        AppUpdateManifest giteeManifest = new(
            _now,
            Stable: Release("1.4.2", isPrerelease: false, "https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.2"),
            Preview: null);
        AppUpdateManifest githubManifest = new(
            _now,
            Stable: Release("1.4.2", isPrerelease: false, "https://github.com/ZLiLiang/JiuMi/releases/tag/v1.4.2"),
            Preview: null);

        AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(
            SemanticVersion.Parse("1.4.0"),
            [
                new AppUpdateSourceManifest(_gitee, giteeManifest),
                new AppUpdateSourceManifest(_github, githubManifest),
            ]);

        Assert.NotNull(candidate);
        Assert.Equal(_gitee, candidate!.Source);
        Assert.Equal("https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.4.2", candidate.Release.PageUrl.AbsoluteUri);
    }

    private static AppUpdateRelease Release(string version, bool isPrerelease, string pageUrl) =>
        new(
            SemanticVersion.Parse(version),
            isPrerelease,
            _now,
            new Uri(pageUrl),
            AssetUrl: null,
            AssetSha256: null,
            AssetSize: 0);
}
