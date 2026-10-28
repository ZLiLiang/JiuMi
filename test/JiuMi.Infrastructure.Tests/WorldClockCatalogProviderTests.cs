using System.Globalization;
using JiuMi.Application.WorldClock;
using JiuMi.Infrastructure.WorldClock;
using Serilog;

namespace JiuMi.Infrastructure.Tests;

/// <summary>
/// The fallback chain of the catalog: newest verified download, then the bundled snapshot.
/// </summary>
public sealed class WorldClockCatalogProviderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _settingsDirectory;
    private readonly string _bundledDirectory;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
    private readonly WorldClockCatalogCache _cache;
    private readonly BundledWorldClockCatalogProvider _bundled;

    public WorldClockCatalogProviderTests()
    {
        _settingsDirectory = Path.Combine(_root, "config");
        _bundledDirectory = Path.Combine(_root, "assets");
        Directory.CreateDirectory(_settingsDirectory);
        WorldClockCatalogTestData.WriteBundle(_bundledDirectory, 0, "Bundled");
        _cache = new WorldClockCatalogCache(_settingsDirectory, _logger);
        _bundled = new BundledWorldClockCatalogProvider(_bundledDirectory, _logger);
    }

    public void Dispose()
    {
        _bundled.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_GivenNoDownloadedVersion_ReturnsTheBundledCatalog()
    {
        using var provider = CreateProvider();

        WorldClockCatalog catalog = await provider.LoadAsync();

        Assert.Equal("Bundled", catalog.Cities[0].Name.Zh);
    }

    [Fact]
    public async Task LoadAsync_GivenCachedVersion_ReturnsTheCachedCatalog()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Cached");
        Assert.True(_cache.Write(manifest, files));

        using var provider = CreateProvider();
        WorldClockCatalog catalog = await provider.LoadAsync();

        Assert.Equal("Cached", catalog.Cities[0].Name.Zh);
    }

    [Fact]
    public async Task LoadAsync_GivenNewerCachedVersion_ReloadsWithoutRestart()
    {
        using var provider = CreateProvider();
        Assert.Equal("Bundled", (await provider.LoadAsync()).Cities[0].Name.Zh);

        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Fresh");
        Assert.True(_cache.Write(manifest, files));

        Assert.Equal("Fresh", (await provider.LoadAsync()).Cities[0].Name.Zh);
    }

    [Fact]
    public async Task LoadAsync_GivenCorruptCachedFile_FallsBackToTheBundledCatalog()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Cached");
        Assert.True(_cache.Write(manifest, files));

        string cachedFile = Path.Combine(
            _settingsDirectory,
            "cache",
            "catalog",
            manifest.Version.ToString(CultureInfo.InvariantCulture),
            "cities.json");
        await File.WriteAllTextAsync(cachedFile, """{ "cities": [] }""");

        using var provider = CreateProvider();
        WorldClockCatalog catalog = await provider.LoadAsync();

        Assert.Equal("Bundled", catalog.Cities[0].Name.Zh);
    }

    [Fact]
    public async Task LoadAsync_GivenMemoryOnlySettings_ReturnsTheBundledCatalog()
    {
        var memoryCache = new WorldClockCatalogCache(settingsDirectory: null, _logger);
        using var provider = new WorldClockCatalogProvider(_bundled, memoryCache, _logger);

        WorldClockCatalog catalog = await provider.LoadAsync();

        Assert.Equal("Bundled", catalog.Cities[0].Name.Zh);
    }

    [Fact]
    public async Task Write_GivenNewerVersion_KeepsOnlyTheNewest()
    {
        (WorldClockCatalogManifest older, IReadOnlyDictionary<string, byte[]> olderFiles) =
            WorldClockCatalogTestData.Create(20261027, "Older");
        (WorldClockCatalogManifest newer, IReadOnlyDictionary<string, byte[]> newerFiles) =
            WorldClockCatalogTestData.Create(20261028, "Newer");
        Assert.True(_cache.Write(older, olderFiles));
        Assert.True(_cache.Write(newer, newerFiles));

        string catalogRoot = Path.Combine(_settingsDirectory, "cache", "catalog");
        string[] versions = Directory.GetDirectories(catalogRoot)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["20261028"], versions);
        InstalledCatalogVersion? installed = await _cache.TryReadInstalledAsync(CancellationToken.None);
        Assert.NotNull(installed);
        Assert.Equal(20261028, installed!.Version);
        Assert.Equal(WorldClockCatalogTestData.UpdatedAt, installed.UpdatedAt);
    }

    [Fact]
    public async Task TryReadInstalled_GivenCorruptCachedFile_ReturnsNull()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Cached");
        Assert.True(_cache.Write(manifest, files));

        string cachedFile = Path.Combine(
            _settingsDirectory,
            "cache",
            "catalog",
            manifest.Version.ToString(CultureInfo.InvariantCulture),
            "countries.json");
        File.WriteAllText(cachedFile, "not a catalog");

        Assert.Null(await _cache.TryReadInstalledAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Write_GivenCorruptSameVersionDirectory_ReplacesIt()
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            WorldClockCatalogTestData.Create(20261028, "Cached");
        Assert.True(_cache.Write(manifest, files));

        string cachedFile = Path.Combine(
            _settingsDirectory,
            "cache",
            "catalog",
            manifest.Version.ToString(CultureInfo.InvariantCulture),
            "cities.json");
        File.WriteAllText(cachedFile, "not a catalog");
        Assert.Null(await _cache.TryReadInstalledAsync(CancellationToken.None));

        Assert.True(_cache.Write(manifest, files));

        InstalledCatalogVersion? installed = await _cache.TryReadInstalledAsync(CancellationToken.None);
        Assert.NotNull(installed);
        Assert.Equal(20261028, installed!.Version);
    }

    [Fact]
    public void ReadLastCheckedAt_GivenNoStateFile_ReturnsNull()
    {
        Assert.Null(_cache.ReadLastCheckedAt());
    }

    [Fact]
    public void WriteLastCheckedAt_RoundTrips()
    {
        DateTimeOffset value = WorldClockCatalogTestData.UpdatedAt;

        _cache.WriteLastCheckedAt(value);

        Assert.Equal(value, _cache.ReadLastCheckedAt());
    }

    private WorldClockCatalogProvider CreateProvider() => new(_bundled, _cache, _logger);
}
