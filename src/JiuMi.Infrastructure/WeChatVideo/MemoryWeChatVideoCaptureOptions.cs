namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Polling cadence and retry timings for <see cref="MemoryWeChatVideoCapture"/>.
/// </summary>
/// <param name="MetadataPollInterval">Interval between Phase 1 silent metadata scans.</param>
/// <param name="PlaybackPollInterval">Interval between Phase 2 <c>NetworkService</c> scans.</param>
/// <param name="TargetedRetryDelay">Delay before retrying a targeted lookup on cache miss.</param>
internal sealed record MemoryWeChatVideoCaptureOptions(
    TimeSpan MetadataPollInterval,
    TimeSpan PlaybackPollInterval,
    TimeSpan TargetedRetryDelay)
{
    /// <summary>
    /// Production timings matching ADR-0018 (2.5 s metadata scan, 500 ms playback poll, 600 ms retry).
    /// </summary>
    public static MemoryWeChatVideoCaptureOptions Default
    {
        get;
    } = new(
        MetadataPollInterval: TimeSpan.FromMilliseconds(2500),
        PlaybackPollInterval: TimeSpan.FromMilliseconds(500),
        TargetedRetryDelay: TimeSpan.FromMilliseconds(600));
}
