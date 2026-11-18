namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Fetches a WeChat video cover into memory, keeping the UI independent of the media host.
/// </summary>
public interface IWeChatVideoThumbnailFetcher
{
    /// <summary>
    /// Fetches the cover image.
    /// </summary>
    /// <param name="thumbnailUrl">The cover URL reported by the page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The image bytes, or a placeholder when the request failed.</returns>
    Task<WeChatVideoThumbnailResult> FetchAsync(
        Uri thumbnailUrl,
        CancellationToken cancellationToken = default);
}
