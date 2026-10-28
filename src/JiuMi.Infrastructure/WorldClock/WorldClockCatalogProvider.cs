using System;
using System.Threading;
using System.Threading.Tasks;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Supplies the newest verified catalog: a downloaded version when one is cached, else the bundled one.
/// </summary>
/// <remarks>
/// The check for a newer cached version is one directory listing, so the page can call this on every
/// activation without re-reading the catalog; the files are only parsed when the version changes.
/// </remarks>
internal sealed class WorldClockCatalogProvider : IWorldClockCatalogProvider, IDisposable
{
    private readonly BundledWorldClockCatalogProvider _bundled;
    private readonly WorldClockCatalogCache _cache;
    private readonly Serilog.ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WorldClockCatalog? _catalog;
    private int _version = WorldClockCatalogVersion.Bundled;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockCatalogProvider"/> class.
    /// </summary>
    /// <param name="bundled">The catalog shipped with the application, used when no download is usable.</param>
    /// <param name="cache">The downloaded catalog versions.</param>
    /// <param name="logger">Receives cache and validation problems.</param>
    /// <remarks><paramref name="bundled"/> stays owned by the caller; this provider only reads through it.</remarks>
    public WorldClockCatalogProvider(
        BundledWorldClockCatalogProvider bundled,
        WorldClockCatalogCache cache,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(bundled);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);

        _bundled = bundled;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WorldClockCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        int latest = _cache.ReadLatestVersion();
        if (_catalog is { } loaded && _version == latest)
        {
            return loaded;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_catalog is { } raced && _version == latest)
            {
                return raced;
            }

            WorldClockCatalog? downloaded = latest > WorldClockCatalogVersion.Bundled
                ? await _cache.LoadAsync(latest, cancellationToken).ConfigureAwait(false)
                : null;
            if (downloaded is not null)
            {
                _logger.Information("Using downloaded world clock catalog version {Version}", latest);
                _catalog = downloaded;
                _version = latest;
                return downloaded;
            }

            _catalog = await _bundled.LoadAsync(cancellationToken).ConfigureAwait(false);
            _version = WorldClockCatalogVersion.Bundled;
            return _catalog;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
