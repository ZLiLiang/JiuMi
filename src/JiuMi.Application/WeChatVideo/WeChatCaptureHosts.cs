namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The page host and the media hosts the capture proxy may decrypt; all other HTTPS stays an
/// opaque tunnel.
/// </summary>
public static class WeChatCaptureHosts
{
    /// <summary>The host serving the page and the Weixin JS bridge.</summary>
    public const string PageHost = "channels.weixin.qq.com";

    /// <summary>The host serving the media and cover files.</summary>
    public const string MediaHost = "finder.video.qq.com";

    /// <summary>
    /// Decides whether a host serves WeChat media, including the CDN variants under
    /// <c>video.qq.com</c>.
    /// </summary>
    /// <param name="host">The request host.</param>
    /// <returns><see langword="true"/> for <c>video.qq.com</c> and its subdomains.</returns>
    public static bool IsMediaHost(string? host)
    {
        return string.Equals(host, "video.qq.com", StringComparison.OrdinalIgnoreCase) ||
            (host is not null &&
             host.EndsWith(".video.qq.com", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Decides whether TLS for a host may be intercepted.
    /// </summary>
    /// <param name="host">The CONNECT target host.</param>
    /// <returns><see langword="true"/> for the page and media hosts only.</returns>
    public static bool ShouldDecrypt(string? host)
    {
        return string.Equals(host, PageHost, StringComparison.OrdinalIgnoreCase) ||
            IsMediaHost(host);
    }
}
