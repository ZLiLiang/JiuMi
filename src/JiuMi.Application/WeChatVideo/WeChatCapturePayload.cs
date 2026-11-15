using System.Globalization;
using System.Text.Json;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Validates the JSON the injected page posts to the loopback listener.
/// </summary>
public static class WeChatCapturePayload
{
    private const string FeedPath = "/web/pages/feed";

    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
    };

    /// <summary>
    /// Parses one captured item, rejecting anything that is not a feed page video.
    /// </summary>
    /// <param name="json">The request body.</param>
    /// <param name="item">The parsed item when the payload is valid.</param>
    /// <returns><see langword="true"/> when the payload is a video from the feed page.</returns>
    public static bool TryCreate(string json, out WeChatVideoCaptureItem? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, _documentOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(root, "kind"), "video", StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryReadFeedPage(ReadString(root, "page"), out Uri page, out string? oid, out string? nid) ||
                !TryReadMediaUrl(ReadString(root, "url"), out string url))
            {
                return false;
            }

            item = new WeChatVideoCaptureItem
            {
                Url = url,
                PageUrl = page.AbsoluteUri,
                DecodeKey = ReadString(root, "decodeKey"),
                ThumbUrl = ReadOptionalMediaUrl(ReadString(root, "thumbUrl")),
                Description = ReadText(root, "description"),
                Uploader = ReadText(root, "uploader"),
                ReportedFileSize = ReadInt64(root, "fileSize"),
                VideoLengthSeconds = ReadInt32(root, "videoLength"),
                Oid = oid,
                Nid = nid,
            };
            return true;
        }
    }

    private static bool TryReadFeedPage(
        string? value,
        out Uri page,
        out string? oid,
        out string? nid)
    {
        oid = null;
        nid = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(parsed.Host, WeChatCaptureHosts.PageHost, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parsed.AbsolutePath, FeedPath, StringComparison.Ordinal))
        {
            page = null!;
            return false;
        }

        page = parsed;
        foreach (string pair in parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=', StringComparison.Ordinal);
            string key = separator < 0 ? pair : pair[..separator];
            string rawValue = separator < 0 ? string.Empty : pair[(separator + 1)..];
            string decimalised;
            try
            {
                decimalised = Uri.UnescapeDataString(rawValue);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (string.Equals(key, "oid", StringComparison.Ordinal))
            {
                oid = string.IsNullOrWhiteSpace(decimalised) ? null : decimalised;
            }
            else if (string.Equals(key, "nid", StringComparison.Ordinal))
            {
                nid = string.IsNullOrWhiteSpace(decimalised) ? null : decimalised;
            }
        }

        return true;
    }

    private static bool TryReadMediaUrl(string? value, out string url)
    {
        url = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(parsed.Host, WeChatCaptureHosts.MediaHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        url = parsed.AbsoluteUri;
        return true;
    }

    private static string? ReadOptionalMediaUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : TryReadMediaUrl(value, out string url) ? url : null;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? ReadText(JsonElement root, string name)
    {
        string? value = ReadString(root, name)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static long? ReadInt64(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long number) => number,
            JsonValueKind.String when long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long parsed) => parsed,
            _ => null,
        };
    }

    private static int? ReadInt32(JsonElement root, string name)
    {
        long? value = ReadInt64(root, name);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }
}
