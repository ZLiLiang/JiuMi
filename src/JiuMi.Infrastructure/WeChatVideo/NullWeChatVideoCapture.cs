using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// The capture contract on platforms where the WinINet chain does not exist.
/// </summary>
internal sealed class NullWeChatVideoCapture : IWeChatVideoCapture
{
    /// <inheritdoc />
    public WeChatVideoCaptureStatus Status => WeChatVideoCaptureStatus.Stopped;

    /// <inheritdoc />
    public bool IsCertificateInstalled => false;

    /// <inheritdoc />
    public event EventHandler<WeChatVideoCaptureItem>? ItemCaptured
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc />
    public event EventHandler<WeChatMediaCapture>? MediaCaptured
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new PlatformNotSupportedException("WeChat video capture requires Windows."));

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<WeChatVideoResidualRestoreResult> RestoreResidualAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(WeChatVideoResidualRestoreResult.Nothing);

    /// <inheritdoc />
    public Task UninstallCertificateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
