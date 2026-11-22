namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Test seam wrapping read-only virtual memory enumeration and reading for a target process.
/// </summary>
internal interface IWeChatProcessMemoryReader
{
    /// <summary>
    /// Checks whether the target process can be opened with minimal read-only permissions.
    /// </summary>
    /// <param name="processId">The target process identifier.</param>
    /// <returns>The access probe result.</returns>
    WeChatProcessAccessStatus ProbeReadAccess(int processId);

    /// <summary>
    /// Enumerates readable private memory regions of the target process and invokes
    /// <paramref name="onChunk"/> for each chunk read.
    /// </summary>
    /// <param name="processId">The target process identifier.</param>
    /// <param name="onChunk">
    /// Callback receiving each read memory slice; return <see langword="false"/> to stop early.
    /// </param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The overall access status for this process.</returns>
    WeChatProcessAccessStatus ScanReadableMemory(
        int processId,
        Func<ReadOnlySpan<byte>, bool> onChunk,
        CancellationToken cancellationToken = default);
}
