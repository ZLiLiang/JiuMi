namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Runs the short-lived MITM capture that reports the video currently open in WeChat.
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
    /// Gets a value indicating whether the capture root certificate is trusted for the current user.
    /// </summary>
    bool IsCertificateInstalled
    {
        get;
    }

    /// <summary>
    /// Occurs when the injected page reports a video opened from the feed page.
    /// The event is raised on a background thread.
    /// </summary>
    event EventHandler<WeChatVideoCaptureItem>? ItemCaptured;

    /// <summary>
    /// Installs the root certificate if needed, starts the local proxy and listener, and sets a local
    /// PAC that sends only the WeChat page host to the local proxy. The first call may show the
    /// Windows certificate trust prompt.
    /// </summary>
    /// <param name="cancellationToken">Cancels the start-up work before the proxy is switched.</param>
    /// <returns>A task that completes when capture is running.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the system proxy, stops the local proxy and listener, removes the state file and
    /// uninstalls the root certificate. Safe to call when capture is not running.
    /// </summary>
    /// <param name="cancellationToken">Cancels the tear-down work.</param>
    /// <returns>A task that completes when capture has stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks <c>cache\wechat\state.json</c> for a system proxy left by a crashed capture and restores it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>What was found and whether it was restored.</returns>
    Task<WeChatVideoResidualRestoreResult> RestoreResidualAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the capture root certificate from the current-user root and personal stores.
    /// The next capture installs a new certificate and asks for trust again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>A task that completes when the certificate is gone.</returns>
    Task UninstallCertificateAsync(CancellationToken cancellationToken = default);
}
