using System.Text;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Two-stage read-only process memory capture for WeChat Finder videos (ADR-0018):
/// Phase 1 silently indexes media metadata into a bounded LRU cache;
/// Phase 2 polls <c>NetworkService</c> and emits only videos actively fetched for playback.
/// </summary>
internal sealed class MemoryWeChatVideoCapture : IWeChatVideoCapture, IDisposable
{
    private readonly IWeChatProcessLocator _processLocator;
    private readonly IWeChatProcessMemoryReader _memoryReader;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly MemoryWeChatVideoCaptureOptions _options;
    private readonly WeChatMetadataCache _metadataCache = new();
    private readonly WeChatPlaybackCorrelator _correlator;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _loopCts;
    private Task? _metadataLoopTask;
    private Task? _playbackLoopTask;
    private int _status = (int)WeChatVideoCaptureStatus.Stopped;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryWeChatVideoCapture"/> class.
    /// </summary>
    /// <param name="processLocator">Locates running WeChat processes and <c>NetworkService</c>.</param>
    /// <param name="memoryReader">Reads private memory regions with minimal read-only rights.</param>
    /// <param name="timeProvider">Supplies delays for the polling loops.</param>
    /// <param name="logger">Records operational diagnostics (never sensitive media tokens or keys).</param>
    /// <param name="options">Optional polling intervals; defaults to <see cref="MemoryWeChatVideoCaptureOptions.Default"/>.</param>
    public MemoryWeChatVideoCapture(
        IWeChatProcessLocator processLocator,
        IWeChatProcessMemoryReader memoryReader,
        TimeProvider timeProvider,
        Serilog.ILogger logger,
        MemoryWeChatVideoCaptureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(processLocator);
        ArgumentNullException.ThrowIfNull(memoryReader);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _processLocator = processLocator;
        _memoryReader = memoryReader;
        _timeProvider = timeProvider;
        _logger = logger;
        _options = options ?? MemoryWeChatVideoCaptureOptions.Default;
        _correlator = new WeChatPlaybackCorrelator(_metadataCache);
    }

    /// <inheritdoc />
    public WeChatVideoCaptureStatus Status => (WeChatVideoCaptureStatus)Volatile.Read(ref _status);

    /// <inheritdoc />
    public event EventHandler<WeChatVideoCaptureItem>? ItemCaptured;

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Status == WeChatVideoCaptureStatus.Running)
            {
                return;
            }

            SetStatus(WeChatVideoCaptureStatus.Starting);
            try
            {
                // Offload Win32 process enumeration, PEB reads and baseline memory scan so the UI thread is never blocked.
                int targetCount = await Task.Run(
                    () =>
                    {
                        IReadOnlyList<WeChatProcessInfo> targets = _processLocator.FindTargetProcesses();
                        ValidatePreflightAccess(targets);

                        _metadataCache.Clear();
                        _correlator.Reset();

                        CaptureInitialBaseline(targets, cancellationToken);
                        return targets.Count;
                    },
                    cancellationToken).ConfigureAwait(false);

                _loopCts = new CancellationTokenSource();
                CancellationToken loopToken = _loopCts.Token;
                _metadataLoopTask = Task.Run(() => RunMetadataLoopAsync(loopToken), CancellationToken.None);
                _playbackLoopTask = Task.Run(() => RunPlaybackLoopAsync(loopToken), CancellationToken.None);

                SetStatus(WeChatVideoCaptureStatus.Running);
                _logger.Information(
                    "WeChat memory video capture started with {ProcessCount} target processes",
                    targetCount);
            }
            catch
            {
                SetStatus(WeChatVideoCaptureStatus.Stopped);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Status == WeChatVideoCaptureStatus.Stopped && _loopCts is null)
            {
                return;
            }

            SetStatus(WeChatVideoCaptureStatus.Stopping);
            await StopLoopsLockedAsync(cancellationToken).ConfigureAwait(false);
            _metadataCache.Clear();
            _correlator.Reset();
            SetStatus(WeChatVideoCaptureStatus.Stopped);
            _logger.Information("WeChat memory video capture stopped");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancellationTokenSource? cts = _loopCts;
        _loopCts = null;
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed during shutdown.
            }

            cts.Dispose();
        }

        _metadataCache.Clear();
        _correlator.Reset();

        // Only dispose the semaphore when uncontended so an in-flight StartAsync/StopAsync
        // can still call Release() cleanly during teardown.
        if (_gate.Wait(0))
        {
            _gate.Dispose();
        }
    }

    private void ValidatePreflightAccess(IReadOnlyList<WeChatProcessInfo> targets)
    {
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                "WeChat is not running. Open WeChat before starting video capture.");
        }

        bool anyAccessible = false;
        bool anyAccessDenied = false;

        foreach (WeChatProcessInfo target in targets)
        {
            WeChatProcessAccessStatus status = _memoryReader.ProbeReadAccess(target.ProcessId);
            if (status == WeChatProcessAccessStatus.Accessible)
            {
                anyAccessible = true;
                break;
            }

            if (status == WeChatProcessAccessStatus.AccessDenied)
            {
                anyAccessDenied = true;
            }
        }

        if (anyAccessible)
        {
            return;
        }

        if (anyAccessDenied)
        {
            throw new UnauthorizedAccessException(
                "Access denied when reading WeChat process memory. Ensure WeChat and JiuMi run at the same privilege level.");
        }

        throw new InvalidOperationException(
            "WeChat processes could not be opened for read-only memory access.");
    }

    private void CaptureInitialBaseline(
        IReadOnlyList<WeChatProcessInfo> targets,
        CancellationToken cancellationToken)
    {
        var baselineEvents = new List<WeChatPlaybackStartEvent>();
        foreach (WeChatProcessInfo target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.IsNetworkService)
            {
                continue;
            }

            _memoryReader.ScanReadableMemory(
                target.ProcessId,
                chunk =>
                {
                    foreach (WeChatPlaybackStartEvent playEvent in
                        WeChatMediaJsonExtractor.ExtractPlaybackStartEvents(chunk))
                    {
                        baselineEvents.Add(playEvent);
                    }

                    foreach (KeyValuePair<string, string> mapping in
                        WeChatMediaJsonExtractor.ExtractFeedToEncFileKeyMappings(chunk))
                    {
                        _correlator.RecordFeedMapping(mapping.Key, mapping.Value);
                    }

                    return true;
                },
                cancellationToken);
        }

        _correlator.SetPlaybackBaseline(baselineEvents);
        _logger.Debug(
            "WeChat memory capture recorded {BaselineCount} baseline playback entries",
            baselineEvents.Count);
    }

    private async Task RunMetadataLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
#pragma warning disable CA1031 // Background polling loop isolates transient scan errors until stopped.
            try
            {
                ScanAndIndexMetadata(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Warning(
                    exception,
                    "WeChat silent metadata indexing pass failed ({ErrorType})",
                    exception.GetType().Name);
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(_options.MetadataPollInterval, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunPlaybackLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.PlaybackPollInterval, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

#pragma warning disable CA1031 // Background polling loop isolates transient scan errors until stopped.
            try
            {
                List<WeChatPlaybackStartEvent> currentEvents = ScanPlaybackEventsAndMappings(cancellationToken);
                if (currentEvents.Count == 0)
                {
                    continue;
                }

                IReadOnlyList<WeChatPlaybackStartEvent> newlyPlayed =
                    _correlator.FilterNewPlaybackStarts(currentEvents);
                foreach (WeChatPlaybackStartEvent playEvent in newlyPlayed)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ResolveAndEmitEventAsync(playEvent, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.Warning(
                    exception,
                    "WeChat playback correlation pass failed ({ErrorType})",
                    exception.GetType().Name);
            }
#pragma warning restore CA1031
        }
    }

    private void ScanAndIndexMetadata(CancellationToken cancellationToken)
    {
        IReadOnlyList<WeChatProcessInfo> targets = _processLocator.FindTargetProcesses();
        foreach (WeChatProcessInfo target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.IsNetworkService)
            {
                continue;
            }

            _memoryReader.ScanReadableMemory(
                target.ProcessId,
                chunk =>
                {
                    IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(chunk);
                    foreach (WeChatVideoCaptureItem item in items)
                    {
                        _metadataCache.Upsert(item);
                        if (!string.IsNullOrWhiteSpace(item.Oid) &&
                            WeChatVideoUrl.TryExtractEncFileKeyPrefix(item.Url, out string prefix))
                        {
                            _correlator.RecordFeedMapping(item.Oid, prefix);
                        }
                    }

                    foreach (KeyValuePair<string, string> mapping in
                        WeChatMediaJsonExtractor.ExtractFeedToEncFileKeyMappings(chunk))
                    {
                        _correlator.RecordFeedMapping(mapping.Key, mapping.Value);
                    }

                    return true;
                },
                cancellationToken);
        }
    }

    private List<WeChatPlaybackStartEvent> ScanPlaybackEventsAndMappings(CancellationToken cancellationToken)
    {
        var events = new List<WeChatPlaybackStartEvent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyList<WeChatProcessInfo> targets = _processLocator.FindTargetProcesses();
        foreach (WeChatProcessInfo target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.IsNetworkService)
            {
                continue;
            }

            _memoryReader.ScanReadableMemory(
                target.ProcessId,
                chunk =>
                {
                    foreach (WeChatPlaybackStartEvent playEvent in
                        WeChatMediaJsonExtractor.ExtractPlaybackStartEvents(chunk))
                    {
                        if (seen.Add(playEvent.EventKey))
                        {
                            events.Add(playEvent);
                        }
                    }

                    foreach (KeyValuePair<string, string> mapping in
                        WeChatMediaJsonExtractor.ExtractFeedToEncFileKeyMappings(chunk))
                    {
                        _correlator.RecordFeedMapping(mapping.Key, mapping.Value);
                    }

                    return true;
                },
                cancellationToken);
        }

        return events;
    }

    private async Task ResolveAndEmitEventAsync(
        WeChatPlaybackStartEvent playEvent,
        CancellationToken cancellationToken)
    {
        string feedId = playEvent.FeedId;
        if (!_correlator.TryGetEncFileKeyPrefix(feedId, out string? encFileKeyPrefix) ||
            string.IsNullOrWhiteSpace(encFileKeyPrefix))
        {
            await Task.Delay(_options.TargetedRetryDelay, _timeProvider, cancellationToken)
                .ConfigureAwait(false);
            _ = ScanPlaybackEventsAndMappings(cancellationToken);

            if (!_correlator.TryGetEncFileKeyPrefix(feedId, out encFileKeyPrefix) ||
                string.IsNullOrWhiteSpace(encFileKeyPrefix))
            {
                _correlator.ReleaseUnresolvedEvent(playEvent);
                return;
            }
        }

        if (_correlator.TryResolveForEvent(playEvent.EventKey, encFileKeyPrefix, out WeChatVideoCaptureItem? cached) &&
            cached is not null)
        {
            RaiseItemCaptured(cached);
            return;
        }

        WeChatVideoCaptureItem? targeted = FindByTargetedScan(encFileKeyPrefix, cancellationToken);
        if (targeted is null || string.IsNullOrWhiteSpace(targeted.DecodeKey))
        {
            await Task.Delay(_options.TargetedRetryDelay, _timeProvider, cancellationToken)
                .ConfigureAwait(false);

            if (_correlator.TryResolveForEvent(playEvent.EventKey, encFileKeyPrefix, out cached) &&
                cached is not null)
            {
                RaiseItemCaptured(cached);
                return;
            }

            WeChatVideoCaptureItem? retried = FindByTargetedScan(encFileKeyPrefix, cancellationToken);
            if (retried is not null &&
                (targeted is null || WeChatMetadataCache.Score(retried) >= WeChatMetadataCache.Score(targeted)))
            {
                targeted = retried;
            }
        }

        if (targeted is not null &&
            _correlator.TryAcceptForEvent(playEvent.EventKey, encFileKeyPrefix, targeted))
        {
            RaiseItemCaptured(targeted);
        }
        else if (targeted is null)
        {
            _correlator.ReleaseUnresolvedEvent(playEvent);
        }
    }

    private WeChatVideoCaptureItem? FindByTargetedScan(
        string encFileKeyPrefix,
        CancellationToken cancellationToken)
    {
        byte[] prefixBytes = Encoding.ASCII.GetBytes(encFileKeyPrefix);
        WeChatVideoCaptureItem? best = null;
        int bestScore = -1;

        IReadOnlyList<WeChatProcessInfo> targets = _processLocator.FindTargetProcesses();
        foreach (WeChatProcessInfo target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.IsNetworkService)
            {
                continue;
            }

            _memoryReader.ScanReadableMemory(
                target.ProcessId,
                chunk =>
                {
                    IReadOnlyList<WeChatVideoCaptureItem> matches =
                        WeChatMediaJsonExtractor.ExtractByEncFileKeyPrefix(chunk, prefixBytes);
                    foreach (WeChatVideoCaptureItem match in matches)
                    {
                        int score = WeChatMetadataCache.Score(match);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = match;
                        }
                    }

                    return true;
                },
                cancellationToken);
        }

        return best;
    }

    private async Task StopLoopsLockedAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cts = _loopCts;
        Task? metadataTask = _metadataLoopTask;
        Task? playbackTask = _playbackLoopTask;

        _loopCts = null;
        _metadataLoopTask = null;
        _playbackLoopTask = null;

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (metadataTask is not null)
        {
            await metadataTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (playbackTask is not null)
        {
            await playbackTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        cts?.Dispose();
    }

    private void RaiseItemCaptured(WeChatVideoCaptureItem item)
    {
        ItemCaptured?.Invoke(this, item);
    }

    private void SetStatus(WeChatVideoCaptureStatus status)
    {
        int next = (int)status;
        if (Interlocked.Exchange(ref _status, next) == next)
        {
            return;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
