namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Decides which page responses receive the capture script.
/// </summary>
public static class WeChatCaptureResponsePolicy
{
    /// <summary>
    /// The largest HTML body the proxy will read and rewrite.
    /// </summary>
    public const int MaxHtmlBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Decides whether a response is the WeChat page that should receive the script.
    /// </summary>
    /// <param name="host">The request host.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="contentType">The response content type, possibly with parameters.</param>
    /// <param name="contentLength">The declared body length, or a negative value when unknown.</param>
    /// <returns><see langword="true"/> for a 200 HTML page of the page host within the size cap.</returns>
    public static bool ShouldInject(
        string? host,
        int statusCode,
        string? contentType,
        long contentLength)
    {
        return string.Equals(host, WeChatCaptureHosts.PageHost, StringComparison.OrdinalIgnoreCase) &&
            statusCode == 200 &&
            contentType is not null &&
            contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            (contentLength < 0 || contentLength <= MaxHtmlBytes);
    }
}
