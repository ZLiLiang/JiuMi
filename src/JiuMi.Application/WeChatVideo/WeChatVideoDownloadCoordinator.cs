namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Turns a session item into a download request, keeps its download state in the session and
/// reports progress to the caller.
/// </summary>
public sealed class WeChatVideoDownloadCoordinator
{
    private readonly WeChatVideoSession _session;
    private readonly WeChatVideoDownloadQueue _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoDownloadCoordinator"/> class.
    /// </summary>
    /// <param name="session">Holds the captured videos and their download states.</param>
    /// <param name="queue">Runs one download at a time.</param>
    public WeChatVideoDownloadCoordinator(
        WeChatVideoSession session,
        WeChatVideoDownloadQueue queue)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(queue);
        _session = session;
        _queue = queue;
    }

    /// <summary>
    /// Queues a download for a captured item and updates the session while it runs.
    /// </summary>
    /// <param name="item">The session item to download.</param>
    /// <param name="outputDirectory">The existing output directory.</param>
    /// <param name="progress">Receives progress after the session item was updated.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The download outcome.</returns>
    public async Task<WeChatVideoDownloadResult> StartAsync(
        WeChatVideoItem item,
        string outputDirectory,
        IProgress<WeChatVideoDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        _session.TryUpdate(
            item.Key,
            current => current with
            {
                State = WeChatVideoDownloadState.Queued,
                Failure = WeChatVideoDownloadFailure.None,
                DownloadedBytes = 0,
                SavedPath = null,
                IsLocalFileMissing = false,
            },
            out WeChatVideoItem? queuedItem);

        WeChatVideoItem effectiveItem = queuedItem ?? item;
        string targetUrl = WeChatVideoUrl.AppendVideoFlag(
            effectiveItem.Capture.Url,
            effectiveItem.SelectedFileFormat);

        var request = new WeChatVideoDownloadRequest
        {
            MediaUrl = new Uri(targetUrl),
            OutputDirectory = outputDirectory,
            FileStem = WeChatVideoFileName.CreateStem(effectiveItem.Capture.Description, effectiveItem.CapturedAt),
            DecodeKey = effectiveItem.Capture.DecodeKey,
        };
        var forwarding = new ForwardingProgress(
            progress,
            value => _session.TryUpdate(
                item.Key,
                current => current with
                {
                    State = WeChatVideoDownloadState.Downloading,
                    DownloadedBytes = value.BytesReceived,
                    TotalBytes = value.TotalBytes,
                },
                out _));

        WeChatVideoDownloadResult result = await _queue
            .EnqueueAsync(request, forwarding, cancellationToken)
            .ConfigureAwait(false);
        ApplyResult(item.Key, result);
        return result;
    }

    private void ApplyResult(string key, WeChatVideoDownloadResult result)
    {
        _session.TryUpdate(
            key,
            current => current with
            {
                State = result.Status switch
                {
                    WeChatVideoDownloadStatus.Completed => WeChatVideoDownloadState.Completed,
                    WeChatVideoDownloadStatus.Expired => WeChatVideoDownloadState.Expired,
                    WeChatVideoDownloadStatus.Canceled => WeChatVideoDownloadState.Canceled,
                    _ => WeChatVideoDownloadState.Failed,
                },
                Failure = result.Status == WeChatVideoDownloadStatus.Failed
                    ? result.Failure
                    : WeChatVideoDownloadFailure.None,
                DownloadedBytes = result.BytesReceived,
                TotalBytes = result.TotalBytes ?? current.TotalBytes,
                SavedPath = result.FilePath,
                IsLocalFileMissing = false,
            },
            out _);
    }

    private sealed class ForwardingProgress : IProgress<WeChatVideoDownloadProgress>
    {
        private readonly IProgress<WeChatVideoDownloadProgress>? _downstream;
        private readonly Action<WeChatVideoDownloadProgress> _update;

        public ForwardingProgress(
            IProgress<WeChatVideoDownloadProgress>? downstream,
            Action<WeChatVideoDownloadProgress> update)
        {
            _downstream = downstream;
            _update = update;
        }

        public void Report(WeChatVideoDownloadProgress value)
        {
            _update(value);
            _downstream?.Report(value);
        }
    }
}
