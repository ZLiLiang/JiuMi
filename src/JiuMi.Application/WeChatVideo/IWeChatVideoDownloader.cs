namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Downloads one captured video to a local file.
/// </summary>
public interface IWeChatVideoDownloader
{
    /// <summary>
    /// Downloads the media to a <c>.part</c> file and renames it when complete.
    /// </summary>
    /// <param name="request">The media link and output location.</param>
    /// <param name="progress">Receives the bytes written after each chunk, when supplied.</param>
    /// <param name="cancellationToken">Cancels the download and removes the partial file.</param>
    /// <returns>The outcome; expected failures are values, not exceptions.</returns>
    Task<WeChatVideoDownloadResult> DownloadAsync(
        WeChatVideoDownloadRequest request,
        IProgress<WeChatVideoDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
