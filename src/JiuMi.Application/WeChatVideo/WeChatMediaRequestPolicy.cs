using System.Globalization;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Recognises the WeChat media request that carries a complete video and reads its total size.
/// </summary>
public static class WeChatMediaRequestPolicy
{
    private const string VideoPath = "/20302/stodownload";

    /// <summary>
    /// Decides whether a response belongs to a full video download rather than a cover or preview.
    /// </summary>
    /// <param name="host">The response host.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns><see langword="true"/> for a 200 or 206 video response.</returns>
    public static bool IsVideoResponse(string? host, string? path, int statusCode)
    {
        return WeChatCaptureHosts.IsMediaHost(host) &&
            path?.Contains(VideoPath, StringComparison.OrdinalIgnoreCase) == true &&
            statusCode is 200 or 206;
    }

    /// <summary>
    /// Reads the full resource length from a <c>Content-Range</c> header, falling back to
    /// <c>Content-Length</c> for a non-ranged response.
    /// </summary>
    /// <param name="contentRange">The <c>Content-Range</c> header, when present.</param>
    /// <param name="contentLength">The response content length.</param>
    /// <param name="totalBytes">The full resource length when it is known.</param>
    /// <returns><see langword="true"/> when a positive total was found.</returns>
    public static bool TryGetTotalBytes(
        string? contentRange,
        long contentLength,
        out long totalBytes)
    {
        totalBytes = 0;
        if (!string.IsNullOrWhiteSpace(contentRange))
        {
            int separator = contentRange.LastIndexOf('/');
            if (separator < 0 ||
                !long.TryParse(
                    contentRange[(separator + 1)..].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long rangedTotal) ||
                rangedTotal <= 0)
            {
                return false;
            }

            totalBytes = rangedTotal;
            return true;
        }

        if (contentLength > 0)
        {
            totalBytes = contentLength;
            return true;
        }

        return false;
    }
}
