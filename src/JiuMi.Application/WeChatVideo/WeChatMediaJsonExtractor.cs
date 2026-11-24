using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Extracts WeChat Finder video metadata JSON objects and <c>NetworkService</c> stream keys
/// from raw process memory buffers.
/// </summary>
public static partial class WeChatMediaJsonExtractor
{
    private const int ContextRadiusBytes = 32768;
    private const int PlayStartContextRadiusBytes = 4096;
    private const int SurroundingBeforeChars = 8192;
    private const int SurroundingAfterChars = 4096;
    private const int MaxEncFileKeyLength = 256;

    private static readonly byte[][] _decodeKeyMarkers =
    [
        "decode_key"u8.ToArray(),
        "decodeKey"u8.ToArray(),
    ];

    private static readonly byte[] _streamMarker = "/20302/stodownload?encfilekey="u8.ToArray();
    private static readonly byte[] _videoPlayStartMarker = "videoPlayStart"u8.ToArray();

    /// <summary>
    /// Scans a memory slice for <c>decode_key</c> / <c>decodeKey</c> anchors and extracts valid
    /// enclosing media objects.
    /// </summary>
    /// <param name="memory">The raw memory bytes read from a WeChat process.</param>
    /// <returns>The extracted media items with valid <c>*.video.qq.com</c> HTTPS URLs.</returns>
    public static IReadOnlyList<WeChatVideoCaptureItem> ExtractByDecodeKey(ReadOnlySpan<byte> memory)
    {
        if (memory.IsEmpty)
        {
            return [];
        }

        var results = new List<WeChatVideoCaptureItem>();
        foreach (byte[] marker in _decodeKeyMarkers)
        {
            ExtractByMarker(memory, marker, allowMissingDecodeKey: false, results);
        }

        return results;
    }

    /// <summary>
    /// Performs a targeted scan for a specific <c>encfilekey</c> prefix, allowing unencrypted videos
    /// whose <c>decode_key</c> is absent or zero.
    /// </summary>
    /// <param name="memory">The raw memory bytes read from a WeChat process.</param>
    /// <param name="encFileKeyPrefix">The ASCII <c>encfilekey</c> prefix to search for.</param>
    /// <returns>The matching media items.</returns>
    public static IReadOnlyList<WeChatVideoCaptureItem> ExtractByEncFileKeyPrefix(
        ReadOnlySpan<byte> memory,
        ReadOnlySpan<byte> encFileKeyPrefix)
    {
        if (memory.IsEmpty || encFileKeyPrefix.IsEmpty)
        {
            return [];
        }

        var results = new List<WeChatVideoCaptureItem>();
        ExtractByMarker(memory, encFileKeyPrefix, allowMissingDecodeKey: true, results);
        return results;
    }

    /// <summary>
    /// Scans a <c>NetworkService</c> memory slice for <c>/20302/stodownload?encfilekey=</c> requests
    /// and returns the distinct 32-character <c>encfilekey</c> prefixes found.
    /// </summary>
    /// <param name="memory">The raw memory bytes read from the <c>NetworkService</c> process.</param>
    /// <returns>Distinct 32-character <c>encfilekey</c> prefixes.</returns>
    public static IReadOnlyList<string> ExtractNetworkServiceEncFileKeyPrefixes(ReadOnlySpan<byte> memory)
    {
        if (memory.Length < _streamMarker.Length + WeChatVideoUrl.EncFileKeyPrefixLength)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var prefixes = new List<string>();
        int start = 0;

        while (start < memory.Length)
        {
            int index = memory[start..].IndexOf(_streamMarker);
            if (index < 0)
            {
                break;
            }

            int valueStart = start + index + _streamMarker.Length;
            int valueEnd = valueStart;
            while (valueEnd < memory.Length && valueEnd - valueStart < MaxEncFileKeyLength)
            {
                byte b = memory[valueEnd];
                if (b == (byte)'&' || b == (byte)'"' || b == (byte)'%' || b < 0x21 || b > 0x7E)
                {
                    break;
                }

                valueEnd++;
            }

            if (valueEnd - valueStart >= WeChatVideoUrl.EncFileKeyPrefixLength)
            {
                string prefix = Encoding.ASCII.GetString(
                    memory.Slice(valueStart, WeChatVideoUrl.EncFileKeyPrefixLength));
                if (seen.Add(prefix))
                {
                    prefixes.Add(prefix);
                }
            }

            start = valueEnd + 1;
        }

        return prefixes;
    }

    /// <summary>
    /// Scans process memory for WeChat LiteApp <c>videoPlayStart</c> telemetry events and extracts
    /// each distinct playback event (including <c>play_id</c> when present so replays of previously
    /// watched videos are not suppressed by the startup baseline).
    /// </summary>
    /// <param name="memory">The raw memory bytes read from a WeChat process.</param>
    /// <returns>Distinct playback start events.</returns>
    public static IReadOnlyList<WeChatPlaybackStartEvent> ExtractPlaybackStartEvents(ReadOnlySpan<byte> memory)
    {
        if (memory.Length < _videoPlayStartMarker.Length)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<WeChatPlaybackStartEvent>();
        int searchStart = 0;

        while (searchStart < memory.Length)
        {
            int index = memory[searchStart..].IndexOf(_videoPlayStartMarker);
            if (index < 0)
            {
                break;
            }

            int pos = searchStart + index;
            searchStart = pos + _videoPlayStartMarker.Length;

            WeChatPlaybackStartEvent? playEvent = TryExtractPlayStartEvent(memory, pos);
            if (playEvent is not null && seen.Add(playEvent.EventKey))
            {
                events.Add(playEvent);
            }
        }

        return events;
    }

    /// <summary>
    /// Scans process memory for WeChat LiteApp <c>videoPlayStart</c> telemetry events and extracts
    /// the distinct numeric <c>feed_id</c> / <c>feedId</c> values of videos that actually started playing
    /// (excluding pre-buffered upcoming videos).
    /// </summary>
    /// <param name="memory">The raw memory bytes read from a WeChat process.</param>
    /// <returns>Distinct feed identifiers that emitted <c>videoPlayStart</c>.</returns>
    public static IReadOnlyList<string> ExtractPlaybackStartFeedIds(ReadOnlySpan<byte> memory)
    {
        IReadOnlyList<WeChatPlaybackStartEvent> events = ExtractPlaybackStartEvents(memory);
        if (events.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var feedIds = new List<string>(events.Count);
        foreach (WeChatPlaybackStartEvent playEvent in events)
        {
            if (seen.Add(playEvent.FeedId))
            {
                feedIds.Add(playEvent.FeedId);
            }
        }

        return feedIds;
    }

    /// <summary>
    /// Scans process memory for WeChat player report lines linking a numeric <c>feed_id</c> to a
    /// <c>/20302/stodownload?encfilekey=</c> URL on <c>*.video.qq.com</c>.
    /// </summary>
    /// <param name="memory">The raw memory bytes read from a WeChat process.</param>
    /// <returns>Distinct <c>(feedId, encFileKeyPrefix)</c> pairs.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> ExtractFeedToEncFileKeyMappings(ReadOnlySpan<byte> memory)
    {
        if (memory.Length < _streamMarker.Length + WeChatVideoUrl.EncFileKeyPrefixLength)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pairs = new List<KeyValuePair<string, string>>();
        int searchStart = 0;

        while (searchStart < memory.Length)
        {
            int index = memory[searchStart..].IndexOf(_streamMarker);
            if (index < 0)
            {
                break;
            }

            int pos = searchStart + index;
            int valueStart = pos + _streamMarker.Length;
            int valueEnd = valueStart;
            while (valueEnd < memory.Length && valueEnd - valueStart < MaxEncFileKeyLength)
            {
                byte b = memory[valueEnd];
                if (b == (byte)'&' || b == (byte)'"' || b == (byte)'%' || b < 0x21 || b > 0x7E)
                {
                    break;
                }

                valueEnd++;
            }

            searchStart = valueEnd + 1;
            if (valueEnd - valueStart < WeChatVideoUrl.EncFileKeyPrefixLength)
            {
                continue;
            }

            int prefixWindowStart = Math.Max(0, pos - 96);
            string before = Encoding.ASCII.GetString(memory[prefixWindowStart..pos]);
            Match match = FeedUrlPrefixRegex().Match(before);
            if (!match.Success)
            {
                continue;
            }

            string host = match.Groups[2].Value;
            if (!WeChatVideoUrl.IsAllowedMediaHost(host))
            {
                continue;
            }

            string feedId = match.Groups[1].Value;
            string encPrefix = Encoding.ASCII.GetString(
                memory.Slice(valueStart, WeChatVideoUrl.EncFileKeyPrefixLength));
            string dedupeKey = feedId + "|" + encPrefix;
            if (seen.Add(dedupeKey))
            {
                pairs.Add(new KeyValuePair<string, string>(feedId, encPrefix));
            }
        }

        return pairs;
    }

    private static WeChatPlaybackStartEvent? TryExtractPlayStartEvent(ReadOnlySpan<byte> memory, int markerPos)
    {
        int start = Math.Max(0, markerPos - PlayStartContextRadiusBytes);
        int end = Math.Min(memory.Length, markerPos + PlayStartContextRadiusBytes);

        string leftRaw = Encoding.UTF8.GetString(memory[start..markerPos]);
        string rightRaw = Encoding.UTF8.GetString(memory[markerPos..end]);

        // Format 1: compact trace "(videoPlayStart~flow-video~15053834436594306599)"
        Match traceMatch = CompactPlayStartRegex().Match(rightRaw);
        if (traceMatch.Success)
        {
            return new WeChatPlaybackStartEvent(traceMatch.Groups[1].Value, PlayId: null);
        }

        // Format 2: CSV report '{"feedId":"14929217264600422650";...},videoPlayStart,{"playId":"..."}'
        Match csvMatch = CsvPlayStartPrefixRegex().Match(leftRaw);
        if (csvMatch.Success)
        {
            Match playIdMatch = CsvPlayIdSuffixRegex().Match(rightRaw);
            string? csvPlayId = playIdMatch.Success ? playIdMatch.Groups[1].Value : null;
            return new WeChatPlaybackStartEvent(csvMatch.Groups[1].Value, csvPlayId);
        }

        // Format 3: JSON event object containing "action_type":"videoPlayStart" (normal or double-escaped)
        bool isEscaped = IsMarkerInDoubleEscapedJson(leftRaw);

        string left = NormalizeJsonFragment(leftRaw, isEscaped);
        string right = NormalizeJsonFragment(rightRaw, isEscaped);
        int markerCharPos = left.Length;
        string text = left + right;

        if (!TryFindEnclosingJsonObject(text, markerCharPos, out int objStart, out int objEnd))
        {
            return null;
        }

        string eventJson = text[objStart..(objEnd + 1)];
        try
        {
            using JsonDocument doc = JsonDocument.Parse(eventJson);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? actionType = GetStringProperty(root, "action_type", "actionType");
            if (!string.Equals(actionType, "videoPlayStart", StringComparison.Ordinal))
            {
                return null;
            }

            string? playId = null;
            if (root.TryGetProperty("action_info", out JsonElement actionInfo) &&
                actionInfo.ValueKind == JsonValueKind.Object)
            {
                playId = GetStringProperty(actionInfo, "play_id", "playId");
            }

            if (root.TryGetProperty("ele_info", out JsonElement eleInfo) &&
                eleInfo.ValueKind == JsonValueKind.Object)
            {
                string? feedId = GetStringProperty(eleInfo, "feed_id", "feedId");
                if (IsValidNumericFeedId(feedId))
                {
                    return new WeChatPlaybackStartEvent(feedId!, playId);
                }
            }

            string? directFeedId = GetStringProperty(root, "feed_id", "feedId");
            return IsValidNumericFeedId(directFeedId)
                ? new WeChatPlaybackStartEvent(directFeedId!, playId)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValidNumericFeedId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length is < 15 or > 22)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static void ExtractByMarker(
        ReadOnlySpan<byte> memory,
        ReadOnlySpan<byte> marker,
        bool allowMissingDecodeKey,
        List<WeChatVideoCaptureItem> results)
    {
        int searchStart = 0;
        while (searchStart < memory.Length)
        {
            int index = memory[searchStart..].IndexOf(marker);
            if (index < 0)
            {
                break;
            }

            int pos = searchStart + index;
            searchStart = pos + marker.Length;

            WeChatVideoCaptureItem? parsed = TryExtractFromWindow(
                memory,
                pos,
                marker.Length,
                allowMissingDecodeKey);

            if (parsed is not null)
            {
                results.Add(parsed);
            }
        }
    }

    private static WeChatVideoCaptureItem? TryExtractFromWindow(
        ReadOnlySpan<byte> memory,
        int markerPos,
        int markerLength,
        bool allowMissingDecodeKey)
    {
        if (markerPos + markerLength + 3 >= memory.Length)
        {
            return null;
        }

        int contextStart = Math.Max(0, markerPos - ContextRadiusBytes);
        int contextEnd = Math.Min(memory.Length, markerPos + ContextRadiusBytes);

        // Decode left and right halves separately so we know the exact character offset of the marker after unescaping.
        string leftRaw = Encoding.UTF8.GetString(memory[contextStart..markerPos]);
        string rightRaw = Encoding.UTF8.GetString(memory[markerPos..contextEnd]);

        // Detect double-escaping strictly at the marker's own enclosing quote (never across the whole
        // window, because normal JSON media objects frequently contain escaped JSON inside spec[].bypass).
        bool isEscaped = IsMarkerInDoubleEscapedJson(leftRaw);

        string left = NormalizeJsonFragment(leftRaw, isEscaped);
        string right = NormalizeJsonFragment(rightRaw, isEscaped);
        int markerCharPos = left.Length;
        string text = left + right;

        if (!TryFindEnclosingJsonObject(text, markerCharPos, out int objectStart, out int objectEnd))
        {
            return null;
        }

        string mediaJson = text[objectStart..(objectEnd + 1)];
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(mediaJson);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? decodeKey = GetStringProperty(root, "decode_key", "decodeKey");
            if (string.IsNullOrWhiteSpace(decodeKey) ||
                !ulong.TryParse(decodeKey, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedKey) ||
                parsedKey == 0)
            {
                if (!allowMissingDecodeKey)
                {
                    return null;
                }

                decodeKey = null;
            }

            string? url = GetStringProperty(root, "url");
            if (string.IsNullOrWhiteSpace(url) ||
                !url.Contains("stodownload", StringComparison.OrdinalIgnoreCase) ||
                !IsCleanAscii(url) ||
                !WeChatVideoUrl.IsAllowedMediaUrl(url))
            {
                return null;
            }

            string urlToken = GetStringProperty(root, "url_token", "urlToken") ?? string.Empty;
            if (!IsCleanAscii(urlToken))
            {
                urlToken = string.Empty;
            }

            string fullUrl = BuildFullUrl(url, urlToken);
            if (!WeChatVideoUrl.IsAllowedMediaUrl(fullUrl))
            {
                return null;
            }

            string? thumbUrl = GetStringProperty(root, "thumb_url", "thumbUrl", "cover_url", "coverUrl", "full_thumb_url");
            if (thumbUrl is not null && (!IsCleanAscii(thumbUrl) || !WeChatVideoUrl.IsAllowedMediaUrl(thumbUrl)))
            {
                thumbUrl = null;
            }

            long? fileSize = GetInt64Property(root, "file_size", "fileSize");
            int? durationSeconds = GetInt32Property(root, "video_play_len", "videoDuration", "duration");
            List<WeChatVideoSpec> specs = ExtractSpecs(root);

            // Reject bare player-log stubs like {"url":"https://...&token=..."} that lack both
            // decode_key and any real media metadata (they fail with Protocol error on download).
            if (decodeKey is null &&
                thumbUrl is null &&
                fileSize is null &&
                durationSeconds is null &&
                specs.Count == 0)
            {
                return null;
            }

            string beforeObject = text[Math.Max(0, objectStart - SurroundingBeforeChars)..objectStart];
            string afterObject = text[(objectEnd + 1)..Math.Min(text.Length, objectEnd + SurroundingAfterChars)];

            // Scope description/nickname/feedId strictly to the enclosing feed object when "object_desc"
            // is present, so an empty description ("") never steals the previous video's title or id.
            int objectDescIndex = beforeObject.LastIndexOf("\"object_desc\"", StringComparison.Ordinal);
            string descriptionScope = objectDescIndex >= 0 ? beforeObject[objectDescIndex..] : beforeObject;
            string headerScope = objectDescIndex >= 0
                ? beforeObject[Math.Max(0, objectDescIndex - 1024)..objectDescIndex]
                : beforeObject;

            bool hasDescriptionBefore = TryFindClosestMatch(DescriptionRegex(), descriptionScope, preferLast: true, out string? description);
            if (!hasDescriptionBefore && objectDescIndex < 0)
            {
                _ = TryFindClosestMatch(DescriptionRegex(), afterObject, preferLast: false, out description);
            }

            bool hasNicknameBefore = TryFindClosestMatch(NicknameRegex(), headerScope, preferLast: true, out string? nickname);
            if (!hasNicknameBefore && objectDescIndex < 0)
            {
                _ = TryFindClosestMatch(NicknameRegex(), afterObject, preferLast: false, out nickname);
            }

            _ = TryFindClosestMatch(ParentFeedIdRegex(), headerScope, preferLast: true, out string? feedId);

            return new WeChatVideoCaptureItem
            {
                Url = fullUrl,
                DecodeKey = decodeKey,
                ThumbUrl = thumbUrl,
                Description = description,
                Uploader = nickname,
                ReportedFileSize = fileSize,
                VideoLengthSeconds = durationSeconds,
                Oid = feedId,
                Specs = specs,
            };
        }
    }

    private static bool IsMarkerInDoubleEscapedJson(string leftRaw)
    {
        // Walk backwards from the end of leftRaw to the opening quote of the string containing the marker
        // (either right before "decode_key" or right before "https://...encfilekey=").
        int searchLimit = Math.Max(0, leftRaw.Length - 256);
        for (int i = leftRaw.Length - 1; i >= searchLimit; i--)
        {
            if (leftRaw[i] == '"')
            {
                return IsEscapedQuote(leftRaw, i);
            }
        }

        return false;
    }

    private static string NormalizeJsonFragment(string raw, bool isEscaped)
    {
        if (!isEscaped)
        {
            return raw.Replace("\\/", "/", StringComparison.Ordinal);
        }

        // Single-pass one-level JSON string unescape:
        //   \" -> ", \\ -> \, \/ -> /, \n/\r/\t -> space
        // Preserves nested escapes like \\\" -> \" and \\n -> \n.
        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '\\' && i + 1 < raw.Length)
            {
                char next = raw[i + 1];
                switch (next)
                {
                    case '"':
                        sb.Append('"');
                        i++;
                        continue;
                    case '\\':
                        sb.Append('\\');
                        i++;
                        continue;
                    case '/':
                        sb.Append('/');
                        i++;
                        continue;
                    case 'n':
                    case 'r':
                    case 't':
                        sb.Append(' ');
                        i++;
                        continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool TryFindEnclosingJsonObject(string text, int anchorPos, out int start, out int end)
    {
        start = -1;
        end = -1;

        // anchorPos sits inside a JSON string (property name or URL value).
        // Walk backwards to the opening quote of that string so inString starts accurately as false.
        int scanBackStart = anchorPos - 1;
        while (scanBackStart >= 0 && !(text[scanBackStart] == '"' && !IsEscapedQuote(text, scanBackStart)))
        {
            scanBackStart--;
        }

        scanBackStart--;
        if (scanBackStart < 0)
        {
            return false;
        }

        int depth = 0;
        bool inString = false;
        for (int i = scanBackStart; i >= 0; i--)
        {
            char c = text[i];
            if (c == '"' && !IsEscapedQuote(text, i))
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '}')
            {
                depth++;
            }
            else if (c == '{')
            {
                if (depth == 0)
                {
                    start = i;
                    break;
                }

                depth--;
            }
        }

        if (start < 0)
        {
            return false;
        }

        depth = 0;
        inString = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' && !IsEscapedQuote(text, i))
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    end = i;
                    return end > anchorPos;
                }
            }
        }

        return false;
    }

    private static bool IsEscapedQuote(string text, int quoteIndex)
    {
        int backslashes = 0;
        for (int i = quoteIndex - 1; i >= 0 && text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return (backslashes & 1) == 1;
    }

    private static string? GetStringProperty(JsonElement obj, params string[] names)
    {
        foreach (string name in names)
        {
            if (obj.TryGetProperty(name, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString();
                }

                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return prop.GetRawText();
                }
            }
        }

        return null;
    }

    private static long? GetInt64Property(JsonElement obj, params string[] names)
    {
        foreach (string name in names)
        {
            if (obj.TryGetProperty(name, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out long value))
                {
                    return value;
                }

                if (prop.ValueKind == JsonValueKind.String &&
                    long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static int? GetInt32Property(JsonElement obj, params string[] names)
    {
        foreach (string name in names)
        {
            if (obj.TryGetProperty(name, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int value))
                {
                    return value;
                }

                if (prop.ValueKind == JsonValueKind.String &&
                    int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static double? GetDoubleProperty(JsonElement obj, params string[] names)
    {
        foreach (string name in names)
        {
            if (obj.TryGetProperty(name, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out double value))
                {
                    return value;
                }

                if (prop.ValueKind == JsonValueKind.String &&
                    double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static List<WeChatVideoSpec> ExtractSpecs(JsonElement obj)
    {
        if (!obj.TryGetProperty("spec", out JsonElement specElement) ||
            specElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var specs = new List<WeChatVideoSpec>();
        foreach (JsonElement item in specElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? format = GetStringProperty(item, "file_format", "fileFormat");
            string? codingFormat = GetStringProperty(item, "coding_format", "codingFormat");
            int? width = GetInt32Property(item, "width");
            int? height = GetInt32Property(item, "height");
            int? bitRate = GetInt32Property(item, "bit_rate", "bitRate");
            int? videoBitRate = GetInt32Property(item, "video_bitrate", "videoBitRate", "videoBitrate");
            int? audioBitRate = GetInt32Property(item, "audio_bitrate", "audioBitRate", "audioBitrate");
            double? vfps = GetDoubleProperty(item, "vfps");
            long? fileSize = GetInt64Property(item, "file_size", "fileSize");

            if (format is not null || width is not null || height is not null || bitRate is not null || videoBitRate is not null)
            {
                specs.Add(new WeChatVideoSpec
                {
                    FileFormat = format,
                    CodingFormat = codingFormat,
                    Width = width,
                    Height = height,
                    BitRate = bitRate,
                    VideoBitRate = videoBitRate,
                    AudioBitRate = audioBitRate,
                    Vfps = vfps,
                    FileSize = fileSize,
                });
            }
        }

        return specs;
    }

    private static string BuildFullUrl(string url, string urlToken)
    {
        if (string.IsNullOrEmpty(urlToken) ||
            url.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (urlToken.StartsWith('&'))
        {
            return url.Contains('?', StringComparison.Ordinal)
                ? url + urlToken
                : url + "?" + urlToken.TrimStart('&');
        }

        if (urlToken.StartsWith("token=", StringComparison.OrdinalIgnoreCase))
        {
            return url + (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + urlToken;
        }

        return url + (url.Contains('?', StringComparison.Ordinal) ? "&token=" : "?token=") + urlToken;
    }

    private static bool TryFindClosestMatch(Regex regex, string text, bool preferLast, out string? value)
    {
        MatchCollection matches = regex.Matches(text);
        if (matches.Count == 0)
        {
            value = null;
            return false;
        }

        Match chosen = preferLast ? matches[^1] : matches[0];
        string unescaped = UnescapeJsonString(chosen.Groups[1].Value);
        value = string.IsNullOrWhiteSpace(unescaped) ? null : unescaped;
        return true;
    }

    private static bool IsCleanAscii(string value)
    {
        foreach (char c in value)
        {
            if (c < 0x21 || c > 0x7E)
            {
                return false;
            }
        }

        return true;
    }

    private static string UnescapeJsonString(string input)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse($"\"{input}\"");
            string? unescaped = doc.RootElement.GetString();
            return (unescaped ?? input)
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Trim();
        }
        catch (JsonException)
        {
            return input.Trim();
        }
    }

    [GeneratedRegex(@"""description""\s*:\s*""((?:[^""\\]|\\.){0,2048})""", RegexOptions.CultureInvariant)]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex(@"""nickname""\s*:\s*""((?:[^""\\]|\\.){0,512})""", RegexOptions.CultureInvariant)]
    private static partial Regex NicknameRegex();

    [GeneratedRegex(@"""(?:id|objectId|object_id|feedId|feed_id)""\s*:\s*""?(\d{15,22})""?", RegexOptions.CultureInvariant)]
    private static partial Regex ParentFeedIdRegex();

    [GeneratedRegex(@"^videoPlayStart~[A-Za-z0-9_-]{1,48}~(\d{15,22})\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompactPlayStartRegex();

    [GeneratedRegex(@"""(?:feedId|feed_id)""\s*:\s*""(\d{15,22})""[^\r\n]{0,320}?\}\s*,\s*$", RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    private static partial Regex CsvPlayStartPrefixRegex();

    [GeneratedRegex(@"^videoPlayStart\s*,\s*\{[^}]{0,120}""(?:playId|play_id)""\s*:\s*""([A-Za-z0-9-]{8,64})""", RegexOptions.CultureInvariant)]
    private static partial Regex CsvPlayIdSuffixRegex();

    [GeneratedRegex(@"(?:^|[^0-9])(\d{15,22}),https://([A-Za-z0-9.-]+)/\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex FeedUrlPrefixRegex();
}
