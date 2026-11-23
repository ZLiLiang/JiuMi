using System.Diagnostics.CodeAnalysis;
using JiuMi.Application.Localization;
using JiuMi.Application.Notifications;

namespace JiuMi.Application.Updates;

/// <summary>
/// Schedules background update checks after the shell becomes visible (30s initial delay, then
/// one-shot 24-hour intervals) and shows a tray balloon when a new version is discovered.
/// </summary>
public sealed class AppUpdateScheduler : IDisposable
{
    private readonly IAppUpdateChecker _checker;
    private readonly IUserNotification _notifications;
    private readonly IApplicationInfo _applicationInfo;
    private readonly ISystemLauncher _systemLauncher;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _sync = new();

    private Task? _loopTask;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateScheduler"/> class.
    /// </summary>
    /// <param name="checker">Performs the dual-source update check.</param>
    /// <param name="notifications">Displays the tray balloon notification.</param>
    /// <param name="applicationInfo">Provides the application name for the notification title.</param>
    /// <param name="systemLauncher">Opens the release page when the user clicks the notification.</param>
    /// <param name="timeProvider">Supplies the clock and timer for scheduling.</param>
    /// <param name="logger">Logs unexpected background scheduling failures.</param>
    public AppUpdateScheduler(
        IAppUpdateChecker checker,
        IUserNotification notifications,
        IApplicationInfo applicationInfo,
        ISystemLauncher systemLauncher,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(systemLauncher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _checker = checker;
        _notifications = notifications;
        _applicationInfo = applicationInfo;
        _systemLauncher = systemLauncher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the running background schedule task, or a completed task if not started.
    /// </summary>
    public Task Completion
    {
        get
        {
            lock (_sync)
            {
                return _loopTask ?? Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Starts the background update schedule once the shell is visible. Subsequent calls are ignored.
    /// </summary>
    /// <param name="localization">Provides localized notification strings.</param>
    /// <param name="postToUi">Marshals the tray notification call onto the UI thread.</param>
    public void Start(ILocalizationService localization, Action<Action> postToUi)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(postToUi);

        lock (_sync)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _loopTask = RunLoopAsync(localization, postToUi, _cancellation.Token);
        }
    }

    /// <summary>
    /// Runs a single background check, dispatches a notification when requested, and returns the
    /// one-shot delay until the next check.
    /// </summary>
    /// <param name="localization">Provides localized notification strings.</param>
    /// <param name="postToUi">Marshals the tray notification call onto the UI thread.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The delay before the next automatic check should run.</returns>
    public async Task<TimeSpan> RunOnceAsync(
        ILocalizationService localization,
        Action<Action> postToUi,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(postToUi);

        AppUpdateCheckResult result =
            await _checker.CheckAsync(isManual: false, cancellationToken).ConfigureAwait(false);

        if (result.ShouldNotify && result.AvailableUpdate is { } candidate)
        {
            string key = candidate.Release.IsPrerelease
                ? "Shell.Update.Notification.Preview"
                : "Shell.Update.Notification.Stable";
            string message = localization.Format(key, candidate.Release.Version.ToString());
            Uri pageUrl = candidate.Release.PageUrl;

            postToUi(() => _notifications.Show(
                _applicationInfo.Name,
                message,
                () => _systemLauncher.OpenUri(pageUrl)));
        }

        if (result.Status is AppUpdateCheckStatus.Failed or AppUpdateCheckStatus.Disabled)
        {
            return AppUpdateCheckPolicy.Interval;
        }

        return AppUpdateCheckPolicy.GetNextDelay(
            result.LastCheckedAt ?? _checker.Status.LastCheckedAt,
            _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Stops any scheduled background update checks without disposing the underlying cancellation source.
    /// </summary>
    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _cancellation.Cancel();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Background update scheduler must never crash the shell or leave unobserved task exceptions.")]
    private async Task RunLoopAsync(
        ILocalizationService localization,
        Action<Action> postToUi,
        CancellationToken cancellationToken)
    {
        TimeSpan delay = AppUpdateCheckPolicy.StartupDelay;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
                delay = await RunOnceAsync(localization, postToUi, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Background app update check failed unexpectedly");
                delay = AppUpdateCheckPolicy.Interval;
            }
        }
    }
}
