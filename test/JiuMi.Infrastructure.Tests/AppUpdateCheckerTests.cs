using System.Net;
using System.Net.Http;
using System.Text;
using JiuMi.Application;
using JiuMi.Application.Settings;
using JiuMi.Application.Updates;
using JiuMi.Infrastructure.Updates;
using Serilog;

namespace JiuMi.Infrastructure.Tests;

/// <summary>
/// Dual-source update check: fallback, channel filtering, throttle, switch and notification deduplication.
/// </summary>
public sealed class AppUpdateCheckerTests : IDisposable
{
    private static readonly AppUpdateSource _giteeSource = new(
        "Gitee",
        "https://gitee.test/ZLiLiang/JiuMi",
        new Uri("https://gitee.test/ZLiLiang/JiuMi/releases/download/app-update/update.json"));

    private static readonly AppUpdateSource _githubSource = new(
        "GitHub",
        "https://github.test/ZLiLiang/JiuMi",
        new Uri("https://github.test/ZLiLiang/JiuMi/releases/download/app-update/update.json"));

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.UpdateTests." + Guid.NewGuid().ToString("N"));
    private readonly TestSettingsStore _settingsStore;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
    private readonly MutableTimeProvider _timeProvider = new();
    private readonly AppUpdateStateStore _stateStore;

    public AppUpdateCheckerTests()
    {
        Directory.CreateDirectory(_root);
        _settingsStore = new TestSettingsStore(
            SettingsLocation.ForDirectory(SettingsLocationKind.ExecutableDirectory, _root));
        _stateStore = new AppUpdateStateStore(_root, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task CheckAsync_GivenGiteeDownAndGitHubHealthy_FallsBackToGitHub()
    {
        using var handler = new RoutingHandler(uri =>
            uri.Host == "gitee.test"
                ? throw new HttpRequestException("Gitee unreachable")
                : JsonResponse(ManifestJson(
                    stableVersion: "1.5.0",
                    stablePageUrl: "https://github.test/ZLiLiang/JiuMi/releases/tag/v1.5.0")));
        using var checker = CreateChecker("1.4.1", handler);

        AppUpdateCheckResult result = await checker.CheckAsync(isManual: false);

        Assert.Equal(AppUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.AvailableUpdate);
        Assert.Equal(SemanticVersion.Parse("1.5.0"), result.AvailableUpdate!.Release.Version);
        Assert.Equal("GitHub", result.AvailableUpdate.Source.Name);
        Assert.Equal(
            "https://github.test/ZLiLiang/JiuMi/releases/tag/v1.5.0",
            result.AvailableUpdate.Release.PageUrl.AbsoluteUri);
        Assert.True(result.ShouldNotify);
        Assert.Equal(AppUpdateCheckState.UpdateAvailable, checker.Status.State);
    }

    [Fact]
    public async Task CheckAsync_GivenBothSourcesSameVersion_PrefersGitee()
    {
        using var handler = new RoutingHandler(uri =>
            uri.Host == "gitee.test"
                ? JsonResponse(ManifestJson(
                    stableVersion: "1.5.0",
                    stablePageUrl: "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.5.0"))
                : JsonResponse(ManifestJson(
                    stableVersion: "1.5.0",
                    stablePageUrl: "https://github.test/ZLiLiang/JiuMi/releases/tag/v1.5.0")));
        using var checker = CreateChecker("1.4.1", handler);

        AppUpdateCheckResult result = await checker.CheckAsync(isManual: false);

        Assert.Equal(AppUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("Gitee", result.AvailableUpdate!.Source.Name);
        Assert.Equal(
            "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.5.0",
            result.AvailableUpdate.Release.PageUrl.AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_GivenBothSourcesFail_SilentInBackgroundAndFailedInManual()
    {
        using var handler = new RoutingHandler(_ => throw new HttpRequestException("offline"));
        using var checker = CreateChecker("1.4.1", handler);

        AppUpdateCheckResult background = await checker.CheckAsync(isManual: false);

        Assert.Equal(AppUpdateCheckStatus.Failed, background.Status);
        Assert.Equal(AppUpdateCheckState.Idle, checker.Status.State);

        AppUpdateCheckResult manual = await checker.CheckAsync(isManual: true);

        Assert.Equal(AppUpdateCheckStatus.Failed, manual.Status);
        Assert.Equal(AppUpdateCheckState.Failed, checker.Status.State);
    }

    [Fact]
    public async Task CheckAsync_GivenOnlineDataDisabled_BlocksBackgroundButAllowsManual()
    {
        _settingsStore.Current = AppSettings.CreateDefault() with
        {
            OnlineDataEnabled = false,
        };
        using var handler = new RoutingHandler(_ =>
            JsonResponse(ManifestJson(
                stableVersion: "1.4.1",
                stablePageUrl: "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.4.1")));
        using var checker = CreateChecker("1.4.1", handler);

        AppUpdateCheckResult background = await checker.CheckAsync(isManual: false);

        Assert.Equal(AppUpdateCheckStatus.Disabled, background.Status);
        Assert.Equal(0, handler.CallCount);

        AppUpdateCheckResult manual = await checker.CheckAsync(isManual: true);

        Assert.Equal(AppUpdateCheckStatus.UpToDate, manual.Status);
        Assert.True(handler.CallCount > 0);
        Assert.Equal(AppUpdateCheckState.UpToDate, checker.Status.State);
    }

    [Fact]
    public async Task CheckAsync_GivenRecentCheck_SkipsBackgroundAndAllowsManual()
    {
        using var handler = new RoutingHandler(_ =>
            JsonResponse(ManifestJson(
                stableVersion: "1.4.1",
                stablePageUrl: "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.4.1")));
        using var checker = CreateChecker("1.4.1", handler);

        AppUpdateCheckResult first = await checker.CheckAsync(isManual: false);
        Assert.Equal(AppUpdateCheckStatus.UpToDate, first.Status);
        int callsAfterFirst = handler.CallCount;

        _timeProvider.Advance(TimeSpan.FromHours(12));
        AppUpdateCheckResult skipped = await checker.CheckAsync(isManual: false);
        Assert.Equal(AppUpdateCheckStatus.Skipped, skipped.Status);
        Assert.Equal(callsAfterFirst, handler.CallCount);

        AppUpdateCheckResult manual = await checker.CheckAsync(isManual: true);
        Assert.Equal(AppUpdateCheckStatus.UpToDate, manual.Status);
        Assert.True(handler.CallCount > callsAfterFirst);
    }

    [Fact]
    public async Task CheckAsync_GivenSameUpdateVersion_NotifiesOnlyOnceAndPersistsState()
    {
        using var handler = new RoutingHandler(_ =>
            JsonResponse(ManifestJson(
                stableVersion: "1.5.0",
                stablePageUrl: "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.5.0")));
        using var checker = CreateChecker("1.4.1", handler);

        AppUpdateCheckResult first = await checker.CheckAsync(isManual: false);
        Assert.True(first.ShouldNotify);
        Assert.True(File.Exists(Path.Combine(_root, "updates", "state.json")));

        // Manual check never pops a tray balloon.
        AppUpdateCheckResult manual = await checker.CheckAsync(isManual: true);
        Assert.Equal(AppUpdateCheckStatus.UpdateAvailable, manual.Status);
        Assert.False(manual.ShouldNotify);

        // Next day with a fresh checker instance: same version must not notify again.
        _timeProvider.Advance(TimeSpan.FromHours(25));
        using var reloaded = CreateChecker("1.4.1", handler);
        AppUpdateCheckResult second = await reloaded.CheckAsync(isManual: false);
        Assert.Equal(AppUpdateCheckStatus.UpdateAvailable, second.Status);
        Assert.False(second.ShouldNotify);
    }

    [Fact]
    public async Task CheckAsync_GivenPreviewInstalled_SeesNewerStableAndPreview()
    {
        using var handler = new RoutingHandler(_ =>
            JsonResponse(ManifestJson(
                stableVersion: "1.5.0",
                stablePageUrl: "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.5.0",
                previewVersion: "1.5.0-preview.2",
                previewPageUrl: "https://gitee.test/ZLiLiang/JiuMi/releases/tag/v1.5.0-preview.2")));
        using var checker = CreateChecker("1.5.0-preview.1", handler);

        AppUpdateCheckResult result = await checker.CheckAsync(isManual: false);

        Assert.Equal(AppUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(SemanticVersion.Parse("1.5.0"), result.AvailableUpdate!.Release.Version);
        Assert.False(result.AvailableUpdate.Release.IsPrerelease);
    }

    private AppUpdateChecker CreateChecker(string installedVersion, HttpMessageHandler handler) =>
        new(
            _settingsStore,
            _stateStore,
            new TestApplicationInfo(installedVersion, [_giteeSource, _githubSource]),
            _timeProvider,
            _logger,
            handler);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static string ManifestJson(
        string? stableVersion,
        string? stablePageUrl,
        string? previewVersion = null,
        string? previewPageUrl = null)
    {
        string stable = stableVersion is null
            ? "null"
            : $$"""
                {
                  "version": "{{stableVersion}}",
                  "prerelease": false,
                  "publishedAt": "2026-11-22T00:00:00Z",
                  "pageUrl": "{{stablePageUrl}}",
                  "assetUrl": "{{stablePageUrl}}/JiuMi-{{stableVersion}}-win-x64.zip",
                  "assetSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                  "assetSize": 74664753
                }
                """;
        string preview = previewVersion is null
            ? "null"
            : $$"""
                {
                  "version": "{{previewVersion}}",
                  "prerelease": true,
                  "publishedAt": "2026-11-22T00:00:00Z",
                  "pageUrl": "{{previewPageUrl}}",
                  "assetUrl": "{{previewPageUrl}}/JiuMi-{{previewVersion}}-win-x64.zip",
                  "assetSha256": "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
                  "assetSize": 74664753
                }
                """;
        return $$"""
            {
              "generatedAt": "2026-11-22T00:00:00Z",
              "stable": {{stable}},
              "preview": {{preview}}
            }
            """;
    }

    private sealed class RoutingHandler(Func<Uri, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private int _calls;

        public int CallCount => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(responder(request.RequestUri!));
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class TestApplicationInfo(
        string version,
        IReadOnlyList<AppUpdateSource> sources) : IApplicationInfo
    {
        public string Name => "JiuMi";

        public string Version => version;

        public string RepositoryUrl => "https://github.test/ZLiLiang/JiuMi";

        public string GiteeRepositoryUrl => "https://gitee.test/ZLiLiang/JiuMi";

        public IReadOnlyList<AppUpdateSource> UpdateSources => sources;
    }

    private sealed class TestSettingsStore(SettingsLocation location) : ISettingsStore
    {
        public SettingsLocation Location { get; } = location;

        public AppSettings Current { get; set; } = AppSettings.CreateDefault();

        public SettingsLoadResult Load() => new(Current, Location, SettingsLoadIssue.None, null);

        public SettingsSaveResult Save(AppSettings settings)
        {
            Current = settings;
            return SettingsSaveResult.NotPersisted;
        }
    }
}
