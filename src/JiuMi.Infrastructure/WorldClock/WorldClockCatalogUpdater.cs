using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JiuMi.Application;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Checks the catalog published as release assets, verifies it and caches the newest version.
/// </summary>
/// <remarks>
/// The check is silent by design: every failure keeps the installed catalog in use and only writes a
/// warning to the log. Requests go through the system proxy, carry a fixed user agent and are abandoned
/// after the client timeout.
/// </remarks>
internal sealed class WorldClockCatalogUpdater : IWorldClockCatalogUpdater, IDisposable
{
    private const string ReleaseTag = "world-clock-catalog";
    private const string ManifestFileName = "manifest.json";

    private readonly ISettingsStore _settingsStore;
    private readonly WorldClockCatalogCache _cache;
    private readonly IApplicationInfo _applicationInfo;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _statusSync = new();

    private WorldClockCatalogUpdateStatus? _status;

    public WorldClockCatalogUpdater(
        ISettingsStore settingsStore,
        WorldClockCatalogCache cache,
        IApplicationInfo applicationInfo,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
        : this(settingsStore, cache, applicationInfo, timeProvider, logger, handler: null, manifestUri: null)
    {
    }

    internal WorldClockCatalogUpdater(
        ISettingsStore settingsStore,
        WorldClockCatalogCache cache,
        IApplicationInfo applicationInfo,
        TimeProvider timeProvider,
        Serilog.ILogger logger,
        HttpMessageHandler? handler,
        Uri? manifestUri)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _cache = cache;
        _applicationInfo = applicationInfo;
        _timeProvider = timeProvider;
        _logger = logger;
        _client = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);
        _client.Timeout = TimeSpan.FromSeconds(10);
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("JiuMi", ToProductVersion(applicationInfo.Version)));
        _manifestUri = manifestUri;
    }

    private readonly Uri? _manifestUri;

    /// <inheritdoc />
    public WorldClockCatalogUpdateStatus Status
    {
        get
        {
            lock (_statusSync)
            {
                return _status ??= ReadInitialStatus();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public async Task<WorldClockCatalogCheckResult> CheckAsync(
        bool force,
        CancellationToken cancellationToken = default)
    {
        WorldClockCatalogUpdateStatus current = Status;
        if (!_settingsStore.Current.OnlineDataEnabled)
        {
            return new WorldClockCatalogCheckResult(
                WorldClockCatalogCheckStatus.Disabled,
                current.InstalledVersion,
                current.InstalledUpdatedAt);
        }

        if (!_cache.IsAvailable || !await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new WorldClockCatalogCheckResult(
                WorldClockCatalogCheckStatus.Skipped,
                current.InstalledVersion,
                current.InstalledUpdatedAt);
        }

        DateTimeOffset checkedAt = _timeProvider.GetUtcNow();
        try
        {
            if (!force && !WorldClockCatalogUpdatePolicy.IsDue(_cache.ReadLastCheckedAt(), checkedAt))
            {
                return new WorldClockCatalogCheckResult(
                    WorldClockCatalogCheckStatus.Skipped,
                    current.InstalledVersion,
                    current.InstalledUpdatedAt);
            }

            Publish(current with
            {
                IsChecking = true,
            });
            _cache.WriteLastCheckedAt(checkedAt);

            WorldClockCatalogCheckResult result = await CheckCoreAsync(cancellationToken)
                .ConfigureAwait(false);
            Publish(new WorldClockCatalogUpdateStatus(
                IsChecking: false,
                LastCheckedAt: checkedAt,
                InstalledVersion: result.InstalledVersion,
                InstalledUpdatedAt: result.UpdatedAt));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Publish(current with
            {
                IsChecking = false,
                LastCheckedAt = checkedAt,
            });
            throw;
        }
        catch (Exception exception) when (IsCheckFailure(exception))
        {
            _logger.Warning(exception, "World clock catalog check failed");
            Publish(new WorldClockCatalogUpdateStatus(
                IsChecking: false,
                LastCheckedAt: checkedAt,
                InstalledVersion: current.InstalledVersion,
                InstalledUpdatedAt: current.InstalledUpdatedAt));
            return new WorldClockCatalogCheckResult(
                WorldClockCatalogCheckStatus.Failed,
                current.InstalledVersion,
                current.InstalledUpdatedAt,
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _client.Dispose();
        _gate.Dispose();
    }

    private async Task<WorldClockCatalogCheckResult> CheckCoreAsync(CancellationToken cancellationToken)
    {
        Uri manifestUri = GetManifestUri();
        byte[] manifestBytes = await DownloadAsync(manifestUri, cancellationToken).ConfigureAwait(false);
        if (!WorldClockCatalogManifestJson.TryParse(
                WorldClockCatalogText.Decode(manifestBytes),
                out WorldClockCatalogManifest? manifest,
                out string? manifestError) ||
            manifest is null)
        {
            throw new InvalidDataException("The catalog manifest is invalid: " + manifestError);
        }

        int installedVersion = WorldClockCatalogVersion.Bundled;
        DateTimeOffset? installedUpdatedAt = null;
        if (await _cache.TryReadInstalledAsync(cancellationToken).ConfigureAwait(false) is
            InstalledCatalogVersion installed)
        {
            installedVersion = installed.Version;
            installedUpdatedAt = installed.UpdatedAt;
        }

        if (!WorldClockCatalogVersion.IsNewer(manifest.Version, installedVersion))
        {
            return new WorldClockCatalogCheckResult(
                WorldClockCatalogCheckStatus.UpToDate,
                installedVersion,
                installedUpdatedAt);
        }

        var documents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (WorldClockCatalogManifestFile file in manifest.Files)
        {
            byte[] content = await DownloadAsync(
                    new Uri(manifestUri, file.Name),
                    cancellationToken)
                .ConfigureAwait(false);
            if (content.LongLength != file.Size ||
                !WorldClockCatalogIntegrity.Matches(content, file.Sha256))
            {
                throw new InvalidDataException(
                    $"The downloaded {file.Name} does not match the manifest.");
            }

            documents[file.Name] = content;
        }

        if (!WorldClockCatalogJson.TryParse(
                WorldClockCatalogText.Decode(documents["countries.json"]),
                WorldClockCatalogText.Decode(documents["cities.json"]),
                WorldClockCatalogText.Decode(documents["office-hours.json"]),
                out WorldClockCatalog? catalog,
                out IReadOnlyList<string> issues) ||
            catalog is null)
        {
            throw new InvalidDataException("The downloaded catalog could not be parsed.");
        }

        if (issues.Count > 0)
        {
            throw new InvalidDataException(
                $"The downloaded catalog contains {issues.Count} data issues.");
        }

        if (!_cache.Write(manifest, documents))
        {
            throw new IOException("The downloaded catalog could not be cached.");
        }

        _logger.Information(
            "World clock catalog updated to version {Version} from {UpdatedAt:O}",
            manifest.Version,
            manifest.UpdatedAt);
        return new WorldClockCatalogCheckResult(
            WorldClockCatalogCheckStatus.Updated,
            manifest.Version,
            manifest.UpdatedAt);
    }

    private async Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        Exception? lastFailure = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await _client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new InvalidDataException($"The catalog asset {uri.Segments[^1]} is missing.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    lastFailure = new HttpRequestException(
                        $"The catalog host answered {(int)response.StatusCode}.");
                    continue;
                }

                return await response.Content
                    .ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                lastFailure = exception;
            }
            catch (IOException exception)
            {
                lastFailure = exception;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = new TimeoutException("The catalog request timed out.");
            }
        }

        throw new HttpRequestException("The catalog could not be downloaded.", lastFailure);
    }

    private WorldClockCatalogUpdateStatus ReadInitialStatus()
    {
        int version = WorldClockCatalogVersion.Bundled;
        DateTimeOffset? updatedAt = null;
        if (_cache.TryReadManifestVersion(out int cachedVersion, out DateTimeOffset? cachedUpdatedAt))
        {
            version = cachedVersion;
            updatedAt = cachedUpdatedAt;
        }

        return new WorldClockCatalogUpdateStatus(
            IsChecking: false,
            LastCheckedAt: _cache.ReadLastCheckedAt(),
            InstalledVersion: version,
            InstalledUpdatedAt: updatedAt);
    }

    private void Publish(WorldClockCatalogUpdateStatus status)
    {
        lock (_statusSync)
        {
            _status = status;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private Uri GetManifestUri()
    {
        if (_manifestUri is not null)
        {
            return _manifestUri;
        }

        string repository = _applicationInfo.RepositoryUrl.TrimEnd('/');
        return new Uri($"{repository}/releases/download/{ReleaseTag}/{ManifestFileName}");
    }

    private static string ToProductVersion(string version) =>
        string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;

    private static bool IsCheckFailure(Exception exception) =>
        exception is HttpRequestException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or TimeoutException
            or NotSupportedException
            or System.Security.SecurityException;
}
