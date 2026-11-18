namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The two hosts the capture proxy may decrypt; all other HTTPS stays an opaque tunnel.
/// </summary>
public static class WeChatCaptureHosts
{
    /// <summary>The host serving the page and the Weixin JS bridge.</summary>
    public const string PageHost = "channels.weixin.qq.com";

    /// <summary>The host serving the media and cover files.</summary>
    public const string MediaHost = "finder.video.qq.com";

    /// <summary>
    /// Decides whether TLS for a host may be intercepted.
    /// </summary>
    /// <param name="host">The CONNECT target host.</param>
    /// <returns><see langword="true"/> for the page and media hosts only.</returns>
    public static bool ShouldDecrypt(string? host)
    {
        return string.Equals(host, PageHost, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, MediaHost, StringComparison.OrdinalIgnoreCase);
    }
}
