using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using JiuMi.Application;
using JiuMi.Application.Settings;
using JiuMi.Application.Updates;

namespace JiuMi.Infrastructure.Updates;

/// <summary>
/// Checks the published GitHub and Gitee <c>app-update/update.json</c> manifests concurrently and
/// selects the newest eligible release.
/// </summary>
internal sealed class AppUpdateChecker : IAppUpdateChecker, IDisposable
{
    private const int MaxAttempts = 2;

    private readonly ISettingsStore _settingsStore;
    private readonly AppUpdateStateStore _stateStore;
    private readonly IApplicationInfo _applicationInfo;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _statusSync = new();

    private AppUpdateStatus? _status;

    public AppUpdateChecker(
        ISettingsStore settingsStore,
        AppUpdateStateStore stateStore,
        IApplicationInfo applicationInfo,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
        : this(settingsStore, stateStore, applicationInfo, timeProvider, logger, handler: null)
    {
    }

    internal AppUpdateChecker(
        ISettingsStore settingsStore,
        AppUpdateStateStore stateStore,
        IApplicationInfo applicationInfo,
        TimeProvider timeProvider,
        Serilog.ILogger logger,
        HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _stateStore = stateStore;
        _applicationInfo = applicationInfo;
        _timeProvider = timeProvider;
        _logger = logger;
        _client = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);
        _client.Timeout = TimeSpan.FromSeconds(10);
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("JiuMi", ToProductVersion(applicationInfo.Version)));
    }

    /// <inheritdoc />
    public AppUpdateStatus Status
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
    public async Task<AppUpdateCheckResult> CheckAsync(
        bool isManual,
        CancellationToken cancellationToken = default)
    {
        AppUpdateStatus previousStatus = Status;
        if (!isManual && !_settingsStore.Current.OnlineDataEnabled)
        {
            return new AppUpdateCheckResult(
                AppUpdateCheckStatus.Disabled,
                previousStatus.AvailableUpdate,
                ShouldNotify: false,
                previousStatus.LastCheckedAt);
        }

        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new AppUpdateCheckResult(
                AppUpdateCheckStatus.Skipped,
                previousStatus.AvailableUpdate,
                ShouldNotify: false,
                previousStatus.LastCheckedAt);
        }

        try
        {
            AppUpdateState state = _stateStore.Read();
            DateTimeOffset checkedAt = _timeProvider.GetUtcNow();
            if (!isManual && !AppUpdateCheckPolicy.IsDue(state.LastCheckedAt, checkedAt))
            {
                return new AppUpdateCheckResult(
                    AppUpdateCheckStatus.Skipped,
                    previousStatus.AvailableUpdate,
                    ShouldNotify: false,
                    state.LastCheckedAt);
            }

            Publish(previousStatus with
            {
                State = AppUpdateCheckState.Checking,
            });

            IReadOnlyList<AppUpdateSourceManifest> manifests =
                await FetchAllSourcesAsync(cancellationToken).ConfigureAwait(false);

            if (manifests.Count == 0)
            {
                _logger.Warning("App update check failed across all configured sources");
                if (isManual)
                {
                    Publish(new AppUpdateStatus(
                        AppUpdateCheckState.Failed,
                        state.LastCheckedAt,
                        AvailableUpdate: null));
                }
                else
                {
                    // Background failures are silent: restore the previous UI-facing state.
                    Publish(previousStatus);
                }

                return new AppUpdateCheckResult(
                    AppUpdateCheckStatus.Failed,
                    AvailableUpdate: null,
                    ShouldNotify: false,
                    state.LastCheckedAt);
            }

            if (!SemanticVersion.TryParse(_applicationInfo.Version, out SemanticVersion? installedVersion))
            {
                _logger.Warning(
                    "Running version {Version} is not valid SemVer; defaulting to 0.0.0",
                    _applicationInfo.Version);
                installedVersion = SemanticVersion.Parse("0.0.0");
            }

            AppUpdateCandidate? candidate = AppUpdateSelector.SelectBest(installedVersion, manifests);
            if (candidate is null)
            {
                _stateStore.Write(state with
                {
                    LastCheckedAt = checkedAt,
                });
                Publish(new AppUpdateStatus(
                    AppUpdateCheckState.UpToDate,
                    checkedAt,
                    AvailableUpdate: null));
                return new AppUpdateCheckResult(
                    AppUpdateCheckStatus.UpToDate,
                    AvailableUpdate: null,
                    ShouldNotify: false,
                    checkedAt);
            }

            string candidateVersion = candidate.Release.Version.ToString();
            bool shouldNotify = !isManual &&
                !string.Equals(state.LastNotifiedVersion, candidateVersion, StringComparison.Ordinal);

            _stateStore.Write(new AppUpdateState(
                checkedAt,
                shouldNotify ? candidateVersion : state.LastNotifiedVersion));

            Publish(new AppUpdateStatus(
                AppUpdateCheckState.UpdateAvailable,
                checkedAt,
                candidate));

            _logger.Information(
                "App update {Version} available from {Source}",
                candidateVersion,
                candidate.Source.Name);

            return new AppUpdateCheckResult(
                AppUpdateCheckStatus.UpdateAvailable,
                candidate,
                shouldNotify,
                checkedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Publish(previousStatus);
            throw;
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

    private async Task<IReadOnlyList<AppUpdateSourceManifest>> FetchAllSourcesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AppUpdateSource> sources = _applicationInfo.UpdateSources;
        if (sources.Count == 0)
        {
            return [];
        }

        Task<AppUpdateSourceManifest?>[] tasks = new Task<AppUpdateSourceManifest?>[sources.Count];
        for (int index = 0; index < sources.Count; index++)
        {
            tasks[index] = FetchSourceAsync(sources[index], cancellationToken);
        }

        AppUpdateSourceManifest?[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var succeeded = new List<AppUpdateSourceManifest>(results.Length);
        foreach (AppUpdateSourceManifest? result in results)
        {
            if (result is not null)
            {
                succeeded.Add(result);
            }
        }

        return succeeded;
    }

    private async Task<AppUpdateSourceManifest?> FetchSourceAsync(
        AppUpdateSource source,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(source.ManifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warning(
                "Skipping update source {Source} because its manifest URI is not HTTPS",
                source.Name);
            return null;
        }

        Exception? lastFailure = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await _client.GetAsync(source.ManifestUri, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.Warning(
                        "Update manifest was not found on {Source} (HTTP 404)",
                        source.Name);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    lastFailure = new HttpRequestException(
                        $"The update source answered {(int)response.StatusCode}.");
                    continue;
                }

                string json = await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!AppUpdateManifestJson.TryParse(json, out AppUpdateManifest? manifest, out string? error) ||
                    manifest is null)
                {
                    _logger.Warning(
                        "Update manifest from {Source} is invalid: {Reason}",
                        source.Name,
                        error);
                    return null;
                }

                return new AppUpdateSourceManifest(source, manifest);
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
                lastFailure = new TimeoutException("The update manifest request timed out.");
            }
        }

        _logger.Warning(lastFailure, "Update check failed for source {Source}", source.Name);
        return null;
    }

    private AppUpdateStatus ReadInitialStatus()
    {
        AppUpdateState state = _stateStore.Read();
        return state.LastCheckedAt is { } lastCheckedAt
            ? new AppUpdateStatus(AppUpdateCheckState.UpToDate, lastCheckedAt, AvailableUpdate: null)
            : new AppUpdateStatus(AppUpdateCheckState.Idle, LastCheckedAt: null, AvailableUpdate: null);
    }

    private void Publish(AppUpdateStatus status)
    {
        lock (_statusSync)
        {
            _status = status;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string ToProductVersion(string version) =>
        string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
}
