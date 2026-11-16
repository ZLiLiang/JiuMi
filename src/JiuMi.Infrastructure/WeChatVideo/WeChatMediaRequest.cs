using System.Net.Http;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Applies the request headers WeChat accepts for its media and cover hosts.
/// </summary>
internal static class WeChatMediaRequest
{
    private const string Origin = "https://channels.weixin.qq.com";

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/100.0.4896.127 Safari/537.36";

    private static readonly Uri _referer = new("https://channels.weixin.qq.com/");

    /// <summary>
    /// Applies the shared media headers.
    /// </summary>
    /// <param name="request">The outgoing request.</param>
    /// <param name="accept">The Accept header for this media kind.</param>
    public static void Apply(HttpRequestMessage request, string accept)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(accept);

        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Referrer = _referer;
        request.Headers.TryAddWithoutValidation("Origin", Origin);
        request.Headers.Accept.ParseAdd(accept);
    }
}
