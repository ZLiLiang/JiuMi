using System.Net;
using System.Net.Http;
using System.Text;
using JiuMi.Application;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;
using JiuMi.Infrastructure.WorldClock;
using Serilog;

namespace JiuMi.Infrastructure.Tests;

/// <summary>
/// The online catalog check: throttle, switch, verification and cache writes.
/// </summary>
public sealed class WorldClockCatalogUpdaterTests : IDisposable
{
    private static readonly Uri _manifestUri =
        new("https://example.test/releases/download/world-clock-catalog/manifest.json");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly TestSettingsStore _settingsStore;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
    private readonly MutableTimeProvider _timeProvider = new();
    private readonly WorldClockCatalogCache _cache;

    public WorldClockCatalogUpdaterTests()
    {
        Directory.CreateDirectory(_root);
        _settingsStore = new TestSettingsStore(
            SettingsLocation.ForDirectory(SettingsLocationKind.ExecutableDirectory, _root));
        _cache = new WorldClockCatalogCache(_root, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task CheckAsync_GivenNewerVersion_DownloadsAndCachesIt()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(manifest, files);
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Updated, result.Status);
        Assert.Equal(20261028, result.InstalledVersion);
        Assert.Equal(WorldClockCatalogTestData.UpdatedAt, result.UpdatedAt);
        Assert.Equal(4, handler.CallCount);
        InstalledCatalogVersion? installed = await _cache.TryReadInstalledAsync(CancellationToken.None);
        Assert.NotNull(installed);
        Assert.Equal(20261028, installed!.Version);
        Assert.Equal(20261028, updater.Status.InstalledVersion);
        Assert.Equal(_timeProvider.GetUtcNow(), updater.Status.LastCheckedAt);
    }

    [Fact]
    public async Task CheckAsync_GivenSameVersion_DoesNotDownloadFiles()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        Assert.True(_cache.Write(manifest, files));
        using var handler = new CatalogHandler(manifest, files);
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.UpToDate, result.Status);
        Assert.Equal(20261028, result.InstalledVersion);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CheckAsync_GivenRecentCheck_SkipsWithoutRequests()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(manifest, files);
        using var updater = CreateUpdater(handler);

        await updater.CheckAsync(force: false);
        int callsAfterFirst = handler.CallCount;
        _timeProvider.Advance(TimeSpan.FromHours(23));

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Skipped, result.Status);
        Assert.Equal(callsAfterFirst, handler.CallCount);
    }

    [Fact]
    public async Task CheckAsync_GivenForce_IgnoresTheDailyInterval()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(manifest, files);
        using var updater = CreateUpdater(handler);

        await updater.CheckAsync(force: false);
        int callsAfterFirst = handler.CallCount;

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: true);

        Assert.Equal(WorldClockCatalogCheckStatus.UpToDate, result.Status);
        Assert.True(handler.CallCount > callsAfterFirst);
    }

    [Fact]
    public async Task CheckAsync_GivenOnlineDataDisabled_MakesNoRequests()
    {
        _settingsStore.Current = AppSettings.CreateDefault() with
        {
            OnlineDataEnabled = false,
        };
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(manifest, files);
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: true);

        Assert.Equal(WorldClockCatalogCheckStatus.Disabled, result.Status);
        Assert.Equal(0, handler.CallCount);
        Assert.Null(updater.Status.LastCheckedAt);
    }

    [Fact]
    public async Task CheckAsync_GivenHashMismatch_KeepsTheInstalledCatalog()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(manifest, files)
        {
            TamperCities = true,
        };
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Failed, result.Status);
        Assert.NotNull(result.Failure);
        Assert.Null(await _cache.TryReadInstalledAsync(CancellationToken.None));
        Assert.Equal(WorldClockCatalogVersion.Bundled, updater.Status.InstalledVersion);
    }

    [Fact]
    public async Task CheckAsync_GivenNewerVersionFailsVerification_KeepsTheInstalledCatalog()
    {
        (WorldClockCatalogManifest installedManifest, IReadOnlyDictionary<string, byte[]> installedFiles) =
            WorldClockCatalogTestData.Create(20261027, "Installed");
        Assert.True(_cache.Write(installedManifest, installedFiles));

        (WorldClockCatalogManifest remoteManifest, IReadOnlyDictionary<string, byte[]> remoteFiles) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(remoteManifest, remoteFiles)
        {
            TamperCities = true,
        };
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Failed, result.Status);
        InstalledCatalogVersion? installed = await _cache.TryReadInstalledAsync(CancellationToken.None);
        Assert.NotNull(installed);
        Assert.Equal(20261027, installed!.Version);
        Assert.Equal(20261027, updater.Status.InstalledVersion);
    }

    [Fact]
    public async Task CheckAsync_GivenInvalidManifest_FailsWithoutCaching()
    {
        using var handler = new CatalogHandler(_ => Json(HttpStatusCode.OK, """{ "version": 1 }"""));
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Failed, result.Status);
        Assert.Null(await _cache.TryReadInstalledAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsync_GivenServerError_RetriesThenFailsSilently()
    {
        using var handler = new CatalogHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Failed, result.Status);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task CheckAsync_GivenMissingAsset_DoesNotRetry()
    {
        using var handler = new CatalogHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var updater = CreateUpdater(handler);

        WorldClockCatalogCheckResult result = await updater.CheckAsync(force: false);

        Assert.Equal(WorldClockCatalogCheckStatus.Failed, result.Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CheckAsync_GivenCancellation_PropagatesIt()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Remote");
        using var handler = new CatalogHandler(manifest, files);
        using var updater = CreateUpdater(handler);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => updater.CheckAsync(force: false, source.Token));
    }

    private WorldClockCatalogUpdater CreateUpdater(HttpMessageHandler handler) =>
        new(
            _settingsStore,
            _cache,
            new TestApplicationInfo(),
            _timeProvider,
            _logger,
            handler,
            _manifestUri);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private sealed class CatalogHandler : HttpMessageHandler
    {
        private readonly WorldClockCatalogManifest? _manifest;
        private readonly IReadOnlyDictionary<string, byte[]>? _files;
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? _responder;

        public CatalogHandler(
            WorldClockCatalogManifest manifest,
            IReadOnlyDictionary<string, byte[]> files)
        {
            _manifest = manifest;
            _files = files;
        }

        public CatalogHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public bool TamperCities
        {
            get;
            init;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            if (_responder is not null)
            {
                return Task.FromResult(_responder(request));
            }

            string name = request.RequestUri!.Segments[^1];
            if (name == "manifest.json")
            {
                return Task.FromResult(Json(
                    HttpStatusCode.OK,
                    WorldClockCatalogTestData.ManifestJson(_manifest!)));
            }

            if (_files is null || !_files.TryGetValue(name, out byte[]? content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (TamperCities && name == "cities.json")
            {
                content = Encoding.UTF8.GetBytes("""{ "cities": [] }""");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
            });
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class TestApplicationInfo : IApplicationInfo
    {
        public string Name => "JiuMi";

        public string Version => "1.0.0";

        public string RepositoryUrl => "https://github.com/ZLiLiang/JiuMi";
    }

    private sealed class TestSettingsStore : ISettingsStore
    {
        public TestSettingsStore(SettingsLocation location)
        {
            Location = location;
        }

        public SettingsLocation Location
        {
            get;
        }

        public AppSettings Current { get; set; } = AppSettings.CreateDefault();

        public SettingsLoadResult Load() => new(Current, Location, SettingsLoadIssue.None);

        public SettingsSaveResult Save(AppSettings settings)
        {
            Current = settings;
            return SettingsSaveResult.NotPersisted;
        }
    }
}
