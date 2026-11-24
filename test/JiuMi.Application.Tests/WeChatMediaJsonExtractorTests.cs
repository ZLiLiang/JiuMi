using System.Text;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatMediaJsonExtractorTests
{
    private const string EncKeyA = "11111111111111111111111111111111aaaa";
    private const string EncKeyB = "22222222222222222222222222222222bbbb";

    private static readonly System.Text.Json.JsonSerializerOptions _relaxedJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public void ExtractByDecodeKey_GivenNormalJson_ExtractsAllFieldsAndSurroundingContext()
    {
        string payload =
            $$"""
            garbage{"nickname":"创作者A","description":"精彩视频标题","media":[{
                "url":"https://findera4.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyA}}&bizid=1023",
                "url_token":"&token=tok123",
                "decode_key":"9876543210",
                "thumb_url":"https://findernd.video.qq.com/251/20304/stodownload?encfilekey=thumb1",
                "file_size":2097152,
                "video_play_len":45,
                "spec":[{"file_format":"xWT111","width":1080,"height":1920,"bit_rate":3500,"file_size":2097152}]
            }]}tail
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Equal(
            $"https://findera4.video.qq.com/251/20302/stodownload?encfilekey={EncKeyA}&bizid=1023&token=tok123",
            item.Url);
        Assert.Equal("9876543210", item.DecodeKey);
        Assert.Equal("https://findernd.video.qq.com/251/20304/stodownload?encfilekey=thumb1", item.ThumbUrl);
        Assert.Equal("创作者A", item.Uploader);
        Assert.Equal("精彩视频标题", item.Description);
        Assert.Equal(2097152, item.ReportedFileSize);
        Assert.Equal(45, item.VideoLengthSeconds);
        WeChatVideoSpec spec = Assert.Single(item.Specs);
        Assert.Equal("xWT111", spec.FileFormat);
        Assert.Equal(1080, spec.Width);
        Assert.Equal(1920, spec.Height);
        Assert.Equal(3500, spec.BitRate);
    }

    [Fact]
    public void ExtractByDecodeKey_GivenDoubleEscapedJson_UnescapesSingleLevelAndExtracts()
    {
        string inner =
            $$"""{"nickname":"作者B","description":"双转义\"引号\"标题","media":[{"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyB}}","url_token":"token=tok999","decodeKey":"123456789","thumbUrl":"https://finder.video.qq.com/cover.jpg","videoDuration":12}]}""";
        string doubleEscaped = System.Text.Json.JsonSerializer.Serialize(inner, _relaxedJsonOptions);
        string memory = "prefix" + doubleEscaped + "suffix";

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(memory));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Equal(
            $"https://finder.video.qq.com/251/20302/stodownload?encfilekey={EncKeyB}&token=tok999",
            item.Url);
        Assert.Equal("123456789", item.DecodeKey);
        Assert.Equal("https://finder.video.qq.com/cover.jpg", item.ThumbUrl);
        Assert.Equal("作者B", item.Uploader);
        Assert.Equal(12, item.VideoLengthSeconds);
    }

    [Fact]
    public void ExtractByDecodeKey_GivenAdjacentMediaObjects_NeverCrossesObjectBoundary()
    {
        string payload =
            $$"""
            {"media":[
                {"decode_key":"111111111","note":"missing url in first object"},
                {"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyA}}","url_token":"&token=a","decode_key":"222222222"},
                {"url":"https://findernd.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyB}}","url_token":"&token=b","decode_key":"333333333"}
            ]}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.DecodeKey == "222222222" && i.Url.Contains(EncKeyA, StringComparison.Ordinal));
        Assert.Contains(items, i => i.DecodeKey == "333333333" && i.Url.Contains(EncKeyB, StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractByDecodeKey_GivenDisallowedMediaHost_SkipsItemAndClearsDisallowedThumbHost()
    {
        string payload =
            $$"""
            {"media":[
                {"url":"https://evil.example.com/251/20302/stodownload?encfilekey={{EncKeyA}}","decode_key":"111111"},
                {"url":"http://finder.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyA}}","decode_key":"222222"},
                {"url":"https://findera4.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyB}}","decode_key":"333333","thumb_url":"https://evil.example.com/cover.jpg"}
            ]}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Equal("333333", item.DecodeKey);
        Assert.Null(item.ThumbUrl);
    }

    [Fact]
    public void ExtractByEncFileKeyPrefix_GivenUnencryptedVideoWithoutDecodeKey_ExtractsWithNullDecodeKey()
    {
        string payload =
            $$"""
            {"media":[{"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyA}}","url_token":"&token=plain","video_play_len":9}]}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByEncFileKeyPrefix(
            Encoding.UTF8.GetBytes(payload),
            Encoding.ASCII.GetBytes(EncKeyA[..32]));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Null(item.DecodeKey);
        Assert.Contains("token=plain", item.Url, StringComparison.Ordinal);
        Assert.Equal(9, item.VideoLengthSeconds);
    }

    [Fact]
    public void ExtractByEncFileKeyPrefix_GivenBarePlayerLogUrlStubWithoutDecodeKeyOrMetadata_IgnoresStub()
    {
        string payload =
            $$"""[v1.2.61][Flow][currentTime:0][{"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyA}}&token=stub"}]""";

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByEncFileKeyPrefix(
            Encoding.UTF8.GetBytes(payload),
            Encoding.ASCII.GetBytes(EncKeyA[..32]));

        Assert.Empty(items);
    }

    [Fact]
    public void ExtractNetworkServiceEncFileKeyPrefixes_GivenStreamAndCoverUrls_ReturnsOnly20302VideoPrefixes()
    {
        string memory =
            $"GET /251/20302/stodownload?encfilekey={EncKeyA}&token=abc HTTP/1.1\r\n" +
            $"GET /251/20304/stodownload?encfilekey={EncKeyB}&token=cover HTTP/1.1\r\n" +
            $"GET /251/20302/stodownload?encfilekey=tooshort&token=x HTTP/1.1\r\n" +
            $"GET /251/20302/stodownload?encfilekey={EncKeyA}&token=dup HTTP/1.1\r\n";

        IReadOnlyList<string> prefixes = WeChatMediaJsonExtractor.ExtractNetworkServiceEncFileKeyPrefixes(
            Encoding.ASCII.GetBytes(memory));

        string prefix = Assert.Single(prefixes);
        Assert.Equal(EncKeyA[..32], prefix);
    }

    [Fact]
    public void ExtractPlaybackStartFeedIds_GivenJsonEscapedCsvAndTraceEvents_ExtractsFeedIdsWithoutCrossEventBleed()
    {
        string escapedEvent = System.Text.Json.JsonSerializer.Serialize(
            """{"ele_info":{"feed_id":"15053834436594306599","loop":0},"action_type":"videoPlayStart","action_info":{"play_count":1}}""",
            _relaxedJsonOptions);
        string payload =
            """[{"action_type":"videoPlayFinish","ele_info":{"feed_id":"99999999999999999999"}},""" +
            """{"action_type":"videoPlayStart","ele_info":{"feed_id":"14870465628881295534"}}]""" +
            escapedEvent +
            """{"feedId":"14929217264600422650";"spec":{"fileFormat":"xWT126"}},videoPlayStart,{"playCount":1}""" +
            "(videoPlayFinish~flow-video~88888888888888888888)(videoPlayStart~flow-video~15054437520079063765)";

        IReadOnlyList<string> feedIds = WeChatMediaJsonExtractor.ExtractPlaybackStartFeedIds(
            Encoding.UTF8.GetBytes(payload));

        Assert.Equal(4, feedIds.Count);
        Assert.Contains("14870465628881295534", feedIds);
        Assert.Contains("15053834436594306599", feedIds);
        Assert.Contains("14929217264600422650", feedIds);
        Assert.Contains("15054437520079063765", feedIds);
        Assert.DoesNotContain("99999999999999999999", feedIds);
        Assert.DoesNotContain("88888888888888888888", feedIds);
    }

    [Fact]
    public void ExtractPlaybackStartFeedIds_GivenLargeSessionBufferBeforeActionType_StillExtractsFeedId()
    {
        string hugeSessionBuffer = new('A', 1200);
        string payload =
            $$$"""{"ele_info":{"feed_id":"15053834436594306599","loop":0,"session_buffer":"{{{hugeSessionBuffer}}}","spec":{"file_format":"xWT126"}},"action_type":"videoPlayStart","action_info":{"play_count":1}}""";

        IReadOnlyList<string> feedIds = WeChatMediaJsonExtractor.ExtractPlaybackStartFeedIds(
            Encoding.UTF8.GetBytes(payload));

        Assert.Equal(["15053834436594306599"], feedIds);
    }

    [Fact]
    public void ExtractByDecodeKey_GivenUnicodeEscapedLongDescriptionAndParentId_ExtractsDescriptionAndOid()
    {
        // 35 Chinese chars escaped as \uXXXX = 210 ASCII chars (> old 160 char limit)
        string longChineseTitle = "这是一段超过一百六十个转义字符的微信视频号完整测试标题内容包含话题标签";
        string escapedTitle = System.Text.Json.JsonSerializer.Serialize(longChineseTitle);
        string escapedNickname = System.Text.Json.JsonSerializer.Serialize("十二个中文字符的创作者昵称测试");
        string payload =
            $$"""
            {"id":"15053834436594306599","nickname":{{escapedNickname}},"description":{{escapedTitle}},"media":[{
                "url":"https://findera4.video.qq.com/251/20302/stodownload?encfilekey={{EncKeyA}}",
                "url_token":"&token=tok1",
                "decode_key":"9876543210"
            }]}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Equal(longChineseTitle, item.Description);
        Assert.Equal("十二个中文字符的创作者昵称测试", item.Uploader);
        Assert.Equal("15053834436594306599", item.Oid);
    }

    [Fact]
    public void ExtractByDecodeKey_GivenEmptyDescriptionAfterAnotherVideo_DoesNotStealPreviousVideoTitleOrId()
    {
        string payload =
            $$$"""
            {"object":[
                {"id":"15000000000000000001","nickname":"上一位作者","object_desc":{"description":"上一条视频的标题","media":[
                    {"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{{EncKeyA}}}","url_token":"&token=a","decode_key":"111111"}
                ]}},
                {"id":"15008297183594617123","nickname":"优优小福星","username":"v2_test@finder","object_desc":{"description":"","media":[
                    {"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{{EncKeyB}}}","url_token":"&token=b","decode_key":"222222"}
                ]}}
            ]}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        Assert.Equal(2, items.Count);
        WeChatVideoCaptureItem second = Assert.Single(items, i => i.DecodeKey == "222222");
        Assert.Null(second.Description);
        Assert.Equal("优优小福星", second.Uploader);
        Assert.Equal("15008297183594617123", second.Oid);
    }

    [Fact]
    public void ExtractByDecodeKey_GivenNormalJsonWithEscapedBypassFieldInsideSpec_DoesNotCorruptJson()
    {
        string payload =
            $$$"""
            {"id":"15010135602490314451","nickname":"瑶山阿月","object_desc":{"description":"还在为大油头所困扰吗 快来试试这个植物洗发皂吧#瑶山阿月","media":[{
                "url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{{EncKeyA}}}&bizid=1023",
                "url_token":"&token=tok_yaoshan",
                "thumb_url":"https://finder.video.qq.com/251/20304/stodownload?encfilekey=thumb1",
                "video_play_len":187,
                "file_size":180809293,
                "spec":[{"file_format":"xWT111","width":720,"height":1280,"bypass":"{\"rid\":\"1795363084276662\";\"level_order\":100;\"ip_area_id\":\"cn.ml\"}"}],
                "decode_key":"777456746"
            }]}}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Equal("777456746", item.DecodeKey);
        Assert.Equal("瑶山阿月", item.Uploader);
        Assert.Equal("还在为大油头所困扰吗 快来试试这个植物洗发皂吧#瑶山阿月", item.Description);
        Assert.Equal("15010135602490314451", item.Oid);
    }

    [Fact]
    public void ExtractFeedToEncFileKeyMappings_GivenPlayerReportLines_ExtractsValidPairsOnly()
    {
        string memory =
            $"15053834436594306599,https://findera4.video.qq.com/251/20302/stodownload?encfilekey={EncKeyA}&hy=SH,1323848070\r\n" +
            $"14870465628881295534,https://evil.example.com/251/20302/stodownload?encfilekey={EncKeyB}&hy=SH,123\r\n" +
            $"14929217264600422650,https://finder.video.qq.com/251/20304/stodownload?encfilekey={EncKeyB}&hy=SH,123\r\n";

        IReadOnlyList<KeyValuePair<string, string>> pairs = WeChatMediaJsonExtractor.ExtractFeedToEncFileKeyMappings(
            Encoding.ASCII.GetBytes(memory));

        KeyValuePair<string, string> pair = Assert.Single(pairs);
        Assert.Equal("15053834436594306599", pair.Key);
        Assert.Equal(EncKeyA[..32], pair.Value);
    }

    [Fact]
    public void ExtractByDecodeKey_GivenSpecCodingAndBitrateFields_ParsesSnakeCaseAndCamelCase()
    {
        string payload =
            $$$"""
            {"url":"https://finder.video.qq.com/251/20302/stodownload?encfilekey={{{EncKeyA}}}&token=tok1","decode_key":"123456","spec":[
                {"file_format":"xWT101","coding_format":"h265","width":1080,"height":1920,"bit_rate":2200,"video_bitrate":2050,"audio_bitrate":128,"vfps":30.0,"file_size":0},
                {"fileFormat":"xWT111","codingFormat":"h264","width":720,"height":1280,"bitRate":1500,"videoBitRate":1380,"audioBitRate":96,"vfps":29.97}
            ]}
            """;

        IReadOnlyList<WeChatVideoCaptureItem> items = WeChatMediaJsonExtractor.ExtractByDecodeKey(
            Encoding.UTF8.GetBytes(payload));

        WeChatVideoCaptureItem item = Assert.Single(items);
        Assert.Equal(2, item.Specs.Count);

        WeChatVideoSpec first = item.Specs[0];
        Assert.Equal("xWT101", first.FileFormat);
        Assert.Equal("h265", first.CodingFormat);
        Assert.Equal(1080, first.Width);
        Assert.Equal(1920, first.Height);
        Assert.Equal(2200, first.BitRate);
        Assert.Equal(2050, first.VideoBitRate);
        Assert.Equal(128, first.AudioBitRate);
        Assert.Equal(30.0, first.Vfps);

        WeChatVideoSpec second = item.Specs[1];
        Assert.Equal("xWT111", second.FileFormat);
        Assert.Equal("h264", second.CodingFormat);
        Assert.Equal(720, second.Width);
        Assert.Equal(1280, second.Height);
        Assert.Equal(1500, second.BitRate);
        Assert.Equal(1380, second.VideoBitRate);
        Assert.Equal(96, second.AudioBitRate);
        Assert.Equal(29.97, second.Vfps);
    }
}
