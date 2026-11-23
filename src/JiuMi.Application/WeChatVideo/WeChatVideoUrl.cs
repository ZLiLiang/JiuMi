namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Builds the session de-duplication key, validates media hosts and normalises media URLs for comparison.
/// </summary>
public static class WeChatVideoUrl
{
    /// <summary>
    /// The required suffix for WeChat Finder media and cover CDN hosts.
    /// </summary>
    public const string MediaHostSuffix = ".video.qq.com";

    /// <summary>
    /// The minimum and canonical prefix length used to index <c>encfilekey</c> values across phases.
    /// </summary>
    public const int EncFileKeyPrefixLength = 32;

    /// <summary>
    /// Builds the key that recognises the same video when WeChat reports it more than once.
    /// The URL's <c>encfilekey</c> is preferred because WeChat reassigns <c>decode_key</c> and
    /// <c>token</c> across entry points; <c>oid</c>+<c>nid</c> and the URL without its one-time
    /// <c>token</c> serve as fallbacks.
    /// </summary>
    /// <param name="item">The captured video.</param>
    /// <returns>An opaque key suitable for dictionary lookups.</returns>
    public static string CreateDeduplicationKey(WeChatVideoCaptureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (TryExtractEncFileKey(item.Url, out string encFileKey))
        {
            return "enc:" + encFileKey;
        }

        return !string.IsNullOrWhiteSpace(item.Oid) && !string.IsNullOrWhiteSpace(item.Nid)
            ? string.Concat("oid:", item.Oid, "\nnid:", item.Nid)
            : "url:" + RemoveTokenParameter(item.Url);
    }

    /// <summary>
    /// Checks whether a host is an allowed WeChat Finder media or cover CDN host (<c>*.video.qq.com</c>).
    /// </summary>
    /// <param name="host">The candidate host name.</param>
    /// <returns><see langword="true"/> when the host is a subdomain of <c>video.qq.com</c>.</returns>
    public static bool IsAllowedMediaHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Length > MediaHostSuffix.Length &&
            host.EndsWith(MediaHostSuffix, StringComparison.OrdinalIgnoreCase) &&
            host[0] != '.';
    }

    /// <summary>
    /// Checks whether a URL is an HTTPS URL pointing to an allowed WeChat Finder CDN host.
    /// </summary>
    /// <param name="url">The candidate URL.</param>
    /// <returns><see langword="true"/> when the URL uses HTTPS on <c>*.video.qq.com</c>.</returns>
    public static bool IsAllowedMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        return string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            IsAllowedMediaHost(parsed.Host);
    }

    /// <summary>
    /// Extracts the <c>encfilekey</c> query parameter from a media URL.
    /// </summary>
    /// <param name="url">The media URL.</param>
    /// <param name="encFileKey">The extracted <c>encfilekey</c> value when present.</param>
    /// <returns><see langword="true"/> when a non-empty <c>encfilekey</c> parameter was found.</returns>
    public static bool TryExtractEncFileKey(string? url, out string encFileKey)
    {
        encFileKey = string.Empty;
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) ||
            string.IsNullOrEmpty(parsed.Query))
        {
            return false;
        }

        string[] pairs = parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            int separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == pair.Length - 1)
            {
                continue;
            }

            string key = pair[..separator];
            if (!string.Equals(key, "encfilekey", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = pair[(separator + 1)..];
            if (!string.IsNullOrWhiteSpace(value))
            {
                encFileKey = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the canonical 32-character <c>encfilekey</c> prefix from a media URL.
    /// </summary>
    /// <param name="url">The media URL.</param>
    /// <param name="prefix">The 32-character prefix when present and long enough.</param>
    /// <returns><see langword="true"/> when an <c>encfilekey</c> of at least 32 characters was found.</returns>
    public static bool TryExtractEncFileKeyPrefix(string? url, out string prefix)
    {
        if (TryExtractEncFileKey(url, out string encFileKey) &&
            encFileKey.Length >= EncFileKeyPrefixLength)
        {
            prefix = encFileKey[..EncFileKeyPrefixLength];
            return true;
        }

        prefix = string.Empty;
        return false;
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
