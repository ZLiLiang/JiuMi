using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Application.WeChatVideo;
using JiuMi.Errors;
using JiuMi.Threading;
using JiuMi.WeChatVideo;

namespace JiuMi.ViewModels;

/// <summary>
/// The WeChat video download page: the session list, the listener and the download actions.
/// </summary>
public sealed class WeChatVideoViewModel : ViewModelBase, IDisposable
{
    private readonly IWeChatVideoCapture _capture;
    private readonly WeChatVideoSession _session;
    private readonly WeChatVideoDownloadCoordinator _downloads;
    private readonly IWeChatVideoThumbnailFetcher _thumbnailFetcher;
    private readonly ISettingsStore _settingsStore;
    private readonly ILocalizationService _localization;
    private readonly InfoBarViewModel _infoBar;
    private readonly IWeChatVideoCertificatePrompt _certificatePrompt;
    private readonly IErrorWindowPresenter _errorWindowPresenter;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly ErrorRetryRegistry _retryRegistry;
    private readonly WeChatVideoStartupNotice _startupNotice;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly Serilog.ILogger _logger;
    private readonly SemaphoreSlim _listenGate = new(1, 1);
    private readonly Dictionary<string, CancellationTokenSource> _downloadSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ThumbnailRequest> _thumbnailRequests = new(StringComparer.Ordinal);
    private string? _downloadDirectory;
    private bool _isDirectoryMissing;
    private bool _startupNoticeShown;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoViewModel"/> class.
    /// </summary>
    /// <param name="capture">Starts and stops the WeChat capture.</param>
    /// <param name="session">Holds the captured videos for this session.</param>
    /// <param name="downloads">Runs the serial downloads.</param>
    /// <param name="thumbnailFetcher">Fetches the covers into memory.</param>
    /// <param name="settingsStore">Supplies and stores the download directory.</param>
    /// <param name="localization">Supplies the interface text.</param>
    /// <param name="infoBar">Shows the residual-proxy notice.</param>
    /// <param name="certificatePrompt">Asks for certificate trust before the first start.</param>
    /// <param name="errorWindowPresenter">Shows the manual proxy recovery failure.</param>
    /// <param name="errorReporter">Receives start and settings failures.</param>
    /// <param name="recordFactory">Creates the records reported by this page.</param>
    /// <param name="retryRegistry">Supplies the in-place retry action for a failed start.</param>
    /// <param name="startupNotice">Carries the residual result from the startup sequence.</param>
    /// <param name="uiThreadDispatcher">Marshals session and capture events to the interface thread.</param>
    /// <param name="logger">Receives capture, cover and download diagnostics.</param>
    public WeChatVideoViewModel(
        IWeChatVideoCapture capture,
        WeChatVideoSession session,
        WeChatVideoDownloadCoordinator downloads,
        IWeChatVideoThumbnailFetcher thumbnailFetcher,
        ISettingsStore settingsStore,
        ILocalizationService localization,
        InfoBarViewModel infoBar,
        IWeChatVideoCertificatePrompt certificatePrompt,
        IErrorWindowPresenter errorWindowPresenter,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        ErrorRetryRegistry retryRegistry,
        WeChatVideoStartupNotice startupNotice,
        IUiThreadDispatcher uiThreadDispatcher,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentNullException.ThrowIfNull(thumbnailFetcher);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(infoBar);
        ArgumentNullException.ThrowIfNull(certificatePrompt);
        ArgumentNullException.ThrowIfNull(errorWindowPresenter);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(retryRegistry);
        ArgumentNullException.ThrowIfNull(startupNotice);
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _capture = capture;
        _session = session;
        _downloads = downloads;
        _thumbnailFetcher = thumbnailFetcher;
        _settingsStore = settingsStore;
        _localization = localization;
        _infoBar = infoBar;
        _certificatePrompt = certificatePrompt;
        _errorWindowPresenter = errorWindowPresenter;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _retryRegistry = retryRegistry;
        _startupNotice = startupNotice;
        _uiThreadDispatcher = uiThreadDispatcher;
        _logger = logger;
        _downloadDirectory = settingsStore.Current.WeChatVideo.DownloadDirectory;

        _capture.ItemCaptured += OnItemCaptured;
        _capture.StatusChanged += OnCaptureStatusChanged;
        _session.Changed += OnSessionChanged;
        _localization.PropertyChanged += OnLocalizationChanged;
        _retryRegistry.Register(ErrorCodes.WeChatVideoCaptureStartFailed, RetryStartAsync);

        RefreshCaptureState();
        RefreshDirectoryState();
    }

    /// <summary>
    /// Gets the captured videos, newest first.
    /// </summary>
    public ObservableCollection<WeChatVideoRowViewModel> Rows
    {
        get;
    } = [];

    /// <summary>
    /// Gets a value indicating whether the list has any videos.
    /// </summary>
    public bool HasItems => Rows.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the empty state replaces the list.
    /// </summary>
    public bool ShowEmptyState => !HasItems;

    /// <summary>
    /// Gets the session summary line.
    /// </summary>
    public string SummaryText => _localization.Format("Tool.WeChatVideo.Summary", Rows.Count);

    /// <summary>
    /// Gets the list order hint.
    /// </summary>
    public string OrderText => _localization["Tool.WeChatVideo.Order"];

    /// <summary>
    /// Gets a value indicating whether the listener switch is on (starting or running).
    /// </summary>
    public bool IsListening => _capture.Status is
        WeChatVideoCaptureStatus.Starting or
        WeChatVideoCaptureStatus.Running;

    /// <summary>
    /// Gets a value indicating whether the listener switch may be toggled now.
    /// </summary>
    public bool CanToggleListening => _capture.Status is
        WeChatVideoCaptureStatus.Stopped or
        WeChatVideoCaptureStatus.Running;

    /// <summary>
    /// Gets the listener status text.
    /// </summary>
    public string ListenStatusText => _localization[_capture.Status switch
    {
        WeChatVideoCaptureStatus.Starting => "Tool.WeChatVideo.Listen.Starting",
        WeChatVideoCaptureStatus.Running => "Tool.WeChatVideo.Listen.Running",
        WeChatVideoCaptureStatus.Stopping => "Tool.WeChatVideo.Listen.Stopping",
        _ => "Tool.WeChatVideo.Listen.Stopped",
    }];

    /// <summary>
    /// Gets the empty-state title for the current listener state.
    /// </summary>
    public string EmptyTitle => _localization[IsListening
        ? "Tool.WeChatVideo.Empty.Running"
        : "Tool.WeChatVideo.Empty"];

    /// <summary>
    /// Gets the empty-state hint for the current listener state.
    /// </summary>
    public string EmptyHint => _localization[IsListening
        ? "Tool.WeChatVideo.Empty.Running.Hint"
        : "Tool.WeChatVideo.Empty.Hint"];

    /// <summary>
    /// Gets a value indicating whether a download folder is configured.
    /// </summary>
    public bool HasDownloadDirectory => !string.IsNullOrWhiteSpace(_downloadDirectory);

    /// <summary>
    /// Gets a value indicating whether the configured folder no longer exists.
    /// </summary>
    public bool IsDirectoryMissing => _isDirectoryMissing;

    /// <summary>
    /// Gets a value indicating whether the missing-folder banner is shown.
    /// </summary>
    public bool ShowDirectoryMissing => _isDirectoryMissing && HasItems;

    /// <summary>
    /// Gets a value indicating whether starting or retrying a download is allowed.
    /// </summary>
    public bool CanDownload => HasDownloadDirectory && !_isDirectoryMissing;

    /// <summary>
    /// Gets the text of the download folder button.
    /// </summary>
    public string DirectoryLabel
    {
        get
        {
            if (!HasDownloadDirectory)
            {
                return _localization["Tool.WeChatVideo.Directory.Choose"];
            }

            string trimmed = _downloadDirectory!.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }
    }

    /// <summary>
    /// Gets the full download folder path shown by the button tooltip.
    /// </summary>
    public string DirectoryToolTip => HasDownloadDirectory
        ? _downloadDirectory!
        : _localization["Tool.WeChatVideo.Directory.Choose"];

    /// <summary>
    /// Gets the configured download folder, or <see langword="null"/>.
    /// </summary>
    public string? DownloadDirectory => _downloadDirectory;

    /// <summary>
    /// Refreshes the directory and listener state when the page becomes current.
    /// </summary>
    public void Activate()
    {
        if (_disposed)
        {
            return;
        }

        RefreshDirectoryState();
        RefreshCaptureState();
    }

    /// <summary>
    /// Shows the residual-proxy outcome once, after the main window is ready.
    /// </summary>
    public void ShowStartupNoticeOnce()
    {
        if (_startupNoticeShown || _disposed)
        {
            return;
        }

        _startupNoticeShown = true;
        WeChatVideoResidualRestoreResult? result = _startupNotice.Take();
        if (result is null)
        {
            return;
        }

        if (result.Status == WeChatVideoResidualRestoreStatus.Restored)
        {
            _infoBar.ShowSummary(new InfoBarSummary(
                _localization["Tool.WeChatVideo.Residual.Restored"],
                IsWarning: true,
                FolderPath: null));
            return;
        }

        if (result.Status == WeChatVideoResidualRestoreStatus.Failed)
        {
            Exception failure = result.Error ?? new InvalidOperationException(
                "The system proxy left by an interrupted WeChat video capture could not be restored.");
            _logger.Error(
                failure,
                "The system proxy left by an interrupted WeChat video capture could not be restored");
            _errorWindowPresenter.PresentFailure(
                ErrorCodes.WeChatVideoProxyRestoreFailed,
                "WeChatVideo.RestoreResidual",
                failure);
        }
    }

    /// <summary>
    /// Starts or stops the listener. Starting may ask for certificate trust first.
    /// </summary>
    /// <param name="listen">Whether the listener should run.</param>
    /// <returns>A task that completes when the requested state settled.</returns>
    public async Task SetListeningAsync(bool listen)
    {
        if (_disposed)
        {
            return;
        }

        await _listenGate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            if (listen)
            {
                await StartListeningCoreAsync();
            }
            else
            {
                await StopListeningCoreAsync();
            }
        }
        finally
        {
            _listenGate.Release();
            if (!_disposed)
            {
                RefreshCaptureState();
            }
        }
    }

    /// <summary>
    /// Changes the download folder and saves it immediately.
    /// </summary>
    /// <param name="path">The chosen folder, or <see langword="null"/> to clear it.</param>
    public void SetDownloadDirectory(string? path)
    {
        string? normalized = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (string.Equals(_downloadDirectory, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _downloadDirectory = normalized;
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            WeChatVideo = _settingsStore.Current.WeChatVideo with
            {
                DownloadDirectory = normalized,
            },
        });

        if (result.Error is { } error)
        {
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.Save",
                error));
        }

        RefreshDirectoryState();
    }

    /// <summary>
    /// Records a folder-picker failure without breaking the page.
    /// </summary>
    /// <param name="exception">The picker failure.</param>
    public void ReportPickerFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.Warning(exception, "The WeChat video download folder picker failed");
    }

    /// <summary>
    /// Records an unexpected page failure without breaking the page.
    /// </summary>
    /// <param name="exception">The failure.</param>
    public void ReportPageFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.Warning(exception, "The WeChat video download page failed");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.PropertyChanged -= OnLocalizationChanged;
        _capture.ItemCaptured -= OnItemCaptured;
        _capture.StatusChanged -= OnCaptureStatusChanged;
        _session.Changed -= OnSessionChanged;
        _retryRegistry.Unregister(ErrorCodes.WeChatVideoCaptureStartFailed);

        foreach (ThumbnailRequest request in _thumbnailRequests.Values)
        {
            request.Source.Cancel();
        }

        _thumbnailRequests.Clear();
        foreach (CancellationTokenSource source in _downloadSources.Values)
        {
            source.Cancel();
        }

        _downloadSources.Clear();
        foreach (WeChatVideoRowViewModel row in Rows)
        {
            row.Dispose();
        }

        Rows.Clear();
        _listenGate.Dispose();
    }

    private void OnItemCaptured(object? sender, WeChatVideoCaptureItem item)
    {
        if (_disposed)
        {
            return;
        }

#pragma warning disable CA1031 // A capture callback must not take down the proxy listener.
        try
        {
            _session.AddOrUpdate(item);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Adding a captured WeChat video failed");
        }
#pragma warning restore CA1031
    }

    private void OnSessionChanged(object? sender, WeChatVideoSessionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _uiThreadDispatcher.Post(() => ApplySessionChange(e));
    }

    private void OnCaptureStatusChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _uiThreadDispatcher.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            RefreshCaptureState();
            UpdateListState();
        });
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILocalizationService.CurrentLanguage),
            StringComparison.Ordinal))
        {
            return;
        }

        foreach (WeChatVideoRowViewModel row in Rows)
        {
            row.RefreshText();
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(OrderText));
        OnPropertyChanged(nameof(ListenStatusText));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));
        OnPropertyChanged(nameof(DirectoryLabel));
        OnPropertyChanged(nameof(DirectoryToolTip));
    }

    private void ApplySessionChange(WeChatVideoSessionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        switch (e.Change)
        {
            case WeChatVideoSessionChange.Added:
            case WeChatVideoSessionChange.Updated:
                AddOrUpdateRow(e.Item);
                break;
            case WeChatVideoSessionChange.Removed:
                RemoveRow(e.Item.Key);
                break;
        }

        UpdateListState();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The session list owns the row and disposes it when it is removed or the page is disposed.")]
    private void AddOrUpdateRow(WeChatVideoItem item)
    {
        WeChatVideoRowViewModel? row = Rows.FirstOrDefault(
            candidate => string.Equals(candidate.Key, item.Key, StringComparison.Ordinal));
        if (row is null)
        {
            row = new WeChatVideoRowViewModel(item, _localization, CanDownload, HandlePrimaryAction);
            Rows.Insert(0, row);
        }
        else
        {
            row.Update(item);
        }

        UpdateRowSeparators();
        StartThumbnailLoad(row);
    }

    private void RemoveRow(string key)
    {
        WeChatVideoRowViewModel? row = Rows.FirstOrDefault(
            candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }

        Rows.Remove(row);
        CancelThumbnailRequest(key);
        row.Dispose();
        UpdateRowSeparators();
    }

    private void UpdateListState()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(ShowDirectoryMissing));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));

        foreach (WeChatVideoRowViewModel row in Rows)
        {
            row.SetDownloadAvailable(CanDownload);
        }
    }

    private void UpdateRowSeparators()
    {
        for (int index = 0; index < Rows.Count; index++)
        {
            Rows[index].IsLast = index == Rows.Count - 1;
        }
    }

    private void StartThumbnailLoad(WeChatVideoRowViewModel row)
    {
        if (_disposed || row.HasThumbnail || row.ThumbnailUrl is not { } url ||
            string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        if (_thumbnailRequests.TryGetValue(row.Key, out ThumbnailRequest? current))
        {
            if (string.Equals(current.Url, url, StringComparison.Ordinal))
            {
                return;
            }

            current.Source.Cancel();
            _thumbnailRequests.Remove(row.Key);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            _logger.Debug("The WeChat video cover URL is not absolute");
            return;
        }

        var source = new CancellationTokenSource();
        _thumbnailRequests[row.Key] = new ThumbnailRequest(url, source);
        _ = LoadThumbnailAsync(row, uri, source);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The row owns the decoded cover; a cover that arrived after removal is disposed on the UI thread.")]
    private async Task LoadThumbnailAsync(
        WeChatVideoRowViewModel row,
        Uri uri,
        CancellationTokenSource source)
    {
        try
        {
            WeChatVideoThumbnailResult result = await _thumbnailFetcher.FetchAsync(uri, source.Token);
            if (source.IsCancellationRequested || _disposed)
            {
                return;
            }

            Bitmap? thumbnail = null;
            if (!result.IsPlaceholder)
            {
                using var stream = new MemoryStream(result.Bytes);
                thumbnail = new Bitmap(stream);
            }

            _uiThreadDispatcher.Post(() =>
            {
                if (_disposed || !Rows.Contains(row))
                {
                    thumbnail?.Dispose();
                    return;
                }

                row.SetThumbnail(thumbnail);
            });
        }
        catch (OperationCanceledException)
        {
        }
#pragma warning disable CA1031 // A cover is decorative; a failure falls back to the placeholder.
        catch (Exception exception)
        {
            _logger.Debug(exception, "Fetching a WeChat video cover failed");
        }
#pragma warning restore CA1031
        finally
        {
            if (_thumbnailRequests.TryGetValue(row.Key, out ThumbnailRequest? request) &&
                ReferenceEquals(request.Source, source))
            {
                _thumbnailRequests.Remove(row.Key);
            }

            source.Dispose();
        }
    }

    private void CancelThumbnailRequest(string key)
    {
        if (_thumbnailRequests.Remove(key, out ThumbnailRequest? request))
        {
            request.Source.Cancel();
        }
    }

    private void HandlePrimaryAction(WeChatVideoRowViewModel row)
    {
        switch (row.Item.State)
        {
            case WeChatVideoDownloadState.Idle:
            case WeChatVideoDownloadState.Canceled:
            case WeChatVideoDownloadState.Failed:
                _ = StartDownloadAsync(row);
                break;
            case WeChatVideoDownloadState.Queued:
            case WeChatVideoDownloadState.Downloading:
                CancelDownload(row.Key);
                break;
            case WeChatVideoDownloadState.Completed:
            case WeChatVideoDownloadState.Expired:
                _session.Remove(row.Key);
                break;
        }
    }

    private async Task StartDownloadAsync(WeChatVideoRowViewModel row)
    {
        if (_disposed || !CanDownload || _downloadSources.ContainsKey(row.Key))
        {
            return;
        }

        string directory = _downloadDirectory!;
        var source = new CancellationTokenSource();
        _downloadSources[row.Key] = source;
        try
        {
            await _downloads.StartAsync(
                row.Item,
                directory,
                cancellationToken: source.Token);
        }
#pragma warning disable CA1031 // A download failure is already reflected in the row; this is the page boundary.
        catch (Exception exception)
        {
            _logger.Warning(exception, "Starting a WeChat video download failed");
        }
#pragma warning restore CA1031
        finally
        {
            if (_downloadSources.TryGetValue(row.Key, out CancellationTokenSource? current) &&
                ReferenceEquals(current, source))
            {
                _downloadSources.Remove(row.Key);
            }

            source.Dispose();
            RefreshDirectoryState();
        }
    }

    private void CancelDownload(string key)
    {
        if (_downloadSources.TryGetValue(key, out CancellationTokenSource? source))
        {
            source.Cancel();
        }
    }

    private void RefreshDirectoryState()
    {
        _isDirectoryMissing = _downloadDirectory is { } directory &&
            !string.IsNullOrWhiteSpace(directory) &&
            !Directory.Exists(directory);
        OnPropertyChanged(nameof(HasDownloadDirectory));
        OnPropertyChanged(nameof(IsDirectoryMissing));
        OnPropertyChanged(nameof(ShowDirectoryMissing));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(DirectoryLabel));
        OnPropertyChanged(nameof(DirectoryToolTip));
        OnPropertyChanged(nameof(DownloadDirectory));

        foreach (WeChatVideoRowViewModel row in Rows)
        {
            row.SetDownloadAvailable(CanDownload);
        }
    }

    private void RefreshCaptureState()
    {
        OnPropertyChanged(nameof(IsListening));
        OnPropertyChanged(nameof(CanToggleListening));
        OnPropertyChanged(nameof(ListenStatusText));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));
    }

    private async Task<bool> StartListeningCoreAsync()
    {
        if (_capture.Status != WeChatVideoCaptureStatus.Stopped)
        {
            return _capture.Status == WeChatVideoCaptureStatus.Running;
        }

#pragma warning disable CA1031 // The InfoBar is the user-facing outcome of a failed start.
        try
        {
            if (!_capture.IsCertificateInstalled &&
                !await _certificatePrompt.ConfirmInstallAsync())
            {
                return false;
            }

            await _capture.StartAsync();
            return true;
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Starting the WeChat video listener failed");
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.WeChatVideoCaptureStartFailed,
                "WeChatVideo.Start",
                exception));
            return false;
        }
#pragma warning restore CA1031
    }

    private async Task StopListeningCoreAsync()
    {
        if (_capture.Status != WeChatVideoCaptureStatus.Running)
        {
            return;
        }

#pragma warning disable CA1031 // The error window is the user-facing outcome of a failed restore.
        try
        {
            await _capture.StopAsync();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Stopping the WeChat video listener failed");
            _errorWindowPresenter.PresentFailure(
                ErrorCodes.WeChatVideoProxyRestoreFailed,
                "WeChatVideo.Stop",
                exception);
        }
#pragma warning restore CA1031
    }

    private async Task<bool> RetryStartAsync()
    {
        if (_disposed)
        {
            return false;
        }

        await _listenGate.WaitAsync();
        try
        {
            bool started = await StartListeningCoreAsync();
            return started && _capture.Status == WeChatVideoCaptureStatus.Running;
        }
        finally
        {
            _listenGate.Release();
            if (!_disposed)
            {
                RefreshCaptureState();
            }
        }
    }

    private sealed record ThumbnailRequest(string Url, CancellationTokenSource Source);
}
