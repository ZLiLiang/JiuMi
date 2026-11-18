using System.Diagnostics.CodeAnalysis;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Runs downloads one at a time in the order they were enqueued.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The ticket and the UI call this FIFO component a download queue.")]
public sealed class WeChatVideoDownloadQueue : IDisposable, IAsyncDisposable
{
    private readonly IWeChatVideoDownloader _downloader;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _disposeSource = new();
    private Task _tail = Task.CompletedTask;
    private bool _disposed;
    private bool _disposeSourceDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoDownloadQueue"/> class.
    /// </summary>
    /// <param name="downloader">The single-download worker.</param>
    public WeChatVideoDownloadQueue(IWeChatVideoDownloader downloader)
    {
        ArgumentNullException.ThrowIfNull(downloader);
        _downloader = downloader;
    }

    /// <summary>
    /// Adds a download to the end of the queue.
    /// </summary>
    /// <param name="request">The media link and output location.</param>
    /// <param name="progress">Receives progress while the job is running.</param>
    /// <param name="cancellationToken">
    /// Cancels the job whether it is still queued or already running.
    /// </param>
    /// <returns>The job's outcome.</returns>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The queued job owns the linked source and disposes it when it finishes.")]
    public Task<WeChatVideoDownloadResult> EnqueueAsync(
        WeChatVideoDownloadRequest request,
        IProgress<WeChatVideoDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CancellationTokenSource linked;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeSource.Token);
        }

        var completion = new TaskCompletionSource<WeChatVideoDownloadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (linked.IsCancellationRequested)
        {
            linked.Dispose();
            completion.SetResult(WeChatVideoDownloadResult.Canceled());
            return completion.Task;
        }

        CancellationTokenRegistration registration = linked.Token.Register(
            () => completion.TrySetResult(WeChatVideoDownloadResult.Canceled()));
        lock (_gate)
        {
            _tail = _tail
                .ContinueWith(
                    _ => RunAsync(request, progress, linked, registration, completion),
                    CancellationToken.None,
                    TaskContinuationOptions.DenyChildAttach,
                    TaskScheduler.Default)
                .Unwrap();
        }

        return completion.Task;
    }

    /// <summary>
    /// Cancels running and queued jobs without waiting for the queue to drain. The application
    /// host disposes its services synchronously; <see cref="DisposeAsync"/> is the graceful path.
    /// </summary>
    public void Dispose()
    {
        bool cancel;
        lock (_gate)
        {
            cancel = !_disposed;
            _disposed = true;
        }

        if (cancel)
        {
            _disposeSource.Cancel();
        }

        DisposeSource();
    }

    /// <summary>
    /// Cancels running and queued jobs and waits for the queue to drain.
    /// </summary>
    /// <returns>A task that completes when the queue is idle.</returns>
    public async ValueTask DisposeAsync()
    {
        Task tail;
        lock (_gate)
        {
            if (_disposed)
            {
                DisposeSource();
                return;
            }

            _disposed = true;
            tail = _tail;
        }

        _disposeSource.Cancel();
        await WaitForTailAsync(tail).ConfigureAwait(false);
        DisposeSource();
    }

    private async Task RunAsync(
        WeChatVideoDownloadRequest request,
        IProgress<WeChatVideoDownloadProgress>? progress,
        CancellationTokenSource linked,
        CancellationTokenRegistration registration,
        TaskCompletionSource<WeChatVideoDownloadResult> completion)
    {
        try
        {
            if (completion.Task.IsCompleted)
            {
                return;
            }

            // From this point the downloader owns cancellation so that it can delete the partial
            // file before the caller observes the canceled outcome.
            registration.Dispose();
            WeChatVideoDownloadResult result = await _downloader
                .DownloadAsync(request, progress, linked.Token)
                .ConfigureAwait(false);
            completion.TrySetResult(result);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            completion.TrySetResult(WeChatVideoDownloadResult.Canceled());
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            registration.Dispose();
            linked.Dispose();
        }
    }

    private static async Task WaitForTailAsync(Task tail)
    {
        try
        {
            await tail.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DisposeSource()
    {
        lock (_gate)
        {
            if (_disposeSourceDisposed)
            {
                return;
            }

            _disposeSourceDisposed = true;
            _disposeSource.Dispose();
        }
    }
}
