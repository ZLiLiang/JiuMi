namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Builds the session de-duplication key and normalises media URLs for comparison.
/// </summary>
public static class WeChatVideoUrl
{
    /// <summary>
    /// Builds the key that recognises the same video when WeChat reports it more than once.
    /// The feed's <c>oid</c> and <c>nid</c> are authoritative; the URL without its one-time
    /// <c>token</c> is the fallback when either identifier is missing.
    /// </summary>
    /// <param name="item">The captured video.</param>
    /// <returns>An opaque key suitable for dictionary lookups.</returns>
    public static string CreateDeduplicationKey(WeChatVideoCaptureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return !string.IsNullOrWhiteSpace(item.Oid) && !string.IsNullOrWhiteSpace(item.Nid)
            ? string.Concat("oid:", item.Oid, "\nnid:", item.Nid)
            : "url:" + RemoveTokenParameter(item.Url);
    }

    /// <summary>
    /// Removes every <c>token</c> query parameter while keeping the other parameters in place.
    /// </summary>
    /// <param name="url">The media URL.</param>
    /// <returns>The URL without its one-time token.</returns>
    public static string RemoveTokenParameter(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) || string.IsNullOrEmpty(parsed.Query))
        {
            return url;
        }

        string[] pairs = parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        string[] kept = Array.FindAll(pairs, pair => !IsTokenPair(pair));
        if (kept.Length == pairs.Length)
        {
            return url;
        }

        return new UriBuilder(parsed) { Query = string.Join('&', kept) }.Uri.AbsoluteUri;
    }

    private static bool IsTokenPair(string pair)
    {
        int separator = pair.IndexOf('=', StringComparison.Ordinal);
        string key = separator < 0 ? pair : pair[..separator];
        return string.Equals(key, "token", StringComparison.Ordinal);
    }
}
