namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Runs the read-only two-stage memory capture that reports videos actively played in WeChat.
/// </summary>
public interface IWeChatVideoCapture
{
    /// <summary>
    /// Gets the session-only capture state; it always starts as <see cref="WeChatVideoCaptureStatus.Stopped"/>.
    /// </summary>
    WeChatVideoCaptureStatus Status
    {
        get;
    }

    /// <summary>
    /// Occurs when a video actively played in WeChat is correlated with its metadata.
    /// The event is raised on a background thread.
    /// </summary>
    event EventHandler<WeChatVideoCaptureItem>? ItemCaptured;

    /// <summary>
    /// Occurs after <see cref="Status"/> changes. The event is raised on the thread that changed the state.
    /// </summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Validates access to WeChat processes, captures the initial <c>NetworkService</c> baseline,
    /// and starts the background read-only indexing and playback correlation loops.
    /// </summary>
    /// <param name="cancellationToken">Cancels the start-up work.</param>
    /// <returns>A task that completes when capture is running.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the background capture loops and releases session-scoped buffers.
    /// Safe to call when capture is not running.
    /// </summary>
    /// <param name="cancellationToken">Cancels waiting for background tasks to finish.</param>
    /// <returns>A task that completes when capture has stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
