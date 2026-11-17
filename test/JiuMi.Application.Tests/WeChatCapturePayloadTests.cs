using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatCapturePayloadTests
{
    private const string FeedPage =
        "https://channels.weixin.qq.com/web/pages/feed?oid=0O7-RjlWGoQ&nid=HwUEeFRlIP0&entrance_id=1002";

    private const string HomePage =
        "https://channels.weixin.qq.com/web/pages/home?context_id=59-20-140&entrance_id=1020";

    [Fact]
    public void TryCreate_GivenFeedVideo_ReturnsItem()
    {
        string json = $$"""
            {
              "kind": "video",
              "page": "{{FeedPage}}",
              "url": "https://finder.video.qq.com/251/20302/stodownload?token=abc&sign=def",
              "decodeKey": "17000000",
              "videoLength": 27,
              "thumbUrl": "https://finder.video.qq.com/251/20302/thumb",
              "fileSize": 26639859,
              "description": "  A description  ",
              "uploader": "uploader-name"
            }
            """;

        bool created = WeChatCapturePayload.TryCreate(json, out WeChatVideoCaptureItem? item);

        Assert.True(created);
        Assert.NotNull(item);
        Assert.Equal("https://finder.video.qq.com/251/20302/stodownload?token=abc&sign=def", item.Url);
        Assert.Equal("17000000", item.DecodeKey);
        Assert.Equal("https://finder.video.qq.com/251/20302/thumb", item.ThumbUrl);
        Assert.Equal(26_639_859, item.ReportedFileSize);
        Assert.Equal(27, item.VideoLengthSeconds);
        Assert.Equal("A description", item.Description);
        Assert.Equal("uploader-name", item.Uploader);
        Assert.Equal("0O7-RjlWGoQ", item.Oid);
        Assert.Equal("HwUEeFRlIP0", item.Nid);
    }

    [Fact]
    public void TryCreate_GivenDiscoveryFullscreenVideo_ReturnsItem()
    {
        string json = $$"""
            {
              "kind": "video",
              "source": "discover",
              "trigger": "fullscreen",
              "videoId": "15054381593837701158",
              "page": "{{HomePage}}",
              "url": "https://finder.video.qq.com/251/20302/stodownload?token=abc&sign=def",
              "decodeKey": "250614220",
              "videoLength": 345,
              "thumbUrl": "https://finder.video.qq.com/251/20350/thumb",
              "fileSize": 312786968,
              "description": "  曝光5款智商税产品  ",
              "uploader": "uploader-name"
            }
            """;

        bool created = WeChatCapturePayload.TryCreate(json, out WeChatVideoCaptureItem? item);

        Assert.True(created);
        Assert.NotNull(item);
        Assert.Equal("https://finder.video.qq.com/251/20302/stodownload?token=abc&sign=def", item.Url);
        Assert.Equal("250614220", item.DecodeKey);
        Assert.Equal(345, item.VideoLengthSeconds);
        Assert.Equal("曝光5款智商税产品", item.Description);
        Assert.Equal("uploader-name", item.Uploader);
        Assert.Equal("15054381593837701158", item.Oid);
        Assert.Equal("discover", item.Nid);
    }

    [Theory]
    [InlineData(null, "15054381593837701158")]
    [InlineData("preview", "15054381593837701158")]
    [InlineData("discover", null)]
    public void TryCreate_GivenInvalidDiscoveryPayload_ReturnsFalse(string? source, string? videoId)
    {
        string sourceField = source is null ? string.Empty : $"\"source\": \"{source}\",";
        string videoIdField = videoId is null ? string.Empty : $"\"videoId\": \"{videoId}\",";
        string json = $$"""
            {
              "kind": "video",
              {{sourceField}}
              {{videoIdField}}
              "page": "{{HomePage}}",
              "url": "https://finder.video.qq.com/251/20302/stodownload?token=abc"
            }
            """;

        Assert.False(WeChatCapturePayload.TryCreate(json, out _));
    }

    [Fact]
    public void TryCreate_GivenNumericDiscoveryVideoId_UsesItAsTheDeDuplicationKey()
    {
        string json = $$"""
            {
              "kind": "video",
              "source": "discover",
              "videoId": 15054381593837701158,
              "page": "{{HomePage}}",
              "url": "https://finder.video.qq.com/251/20302/stodownload?token=abc"
            }
            """;

        Assert.True(WeChatCapturePayload.TryCreate(json, out WeChatVideoCaptureItem? item));
        Assert.Equal("15054381593837701158", item!.Oid);
        Assert.Equal("discover", item.Nid);
    }

    [Theory]
    [InlineData("https://channels.weixin.qq.com/web/pages/home?oid=1&nid=2")]
    [InlineData("https://channels.weixin.qq.com/web/pages/profile?oid=1&nid=2")]
    public void TryCreate_GivenPageWithoutCaptureSource_ReturnsFalse(string page)
    {
        string json = $$"""
            {
              "kind": "video",
              "page": "{{page}}",
              "url": "https://finder.video.qq.com/media"
            }
            """;

        Assert.False(WeChatCapturePayload.TryCreate(json, out _));
    }

    [Theory]
    [InlineData("https://example.com/web/pages/feed?oid=1&nid=2")]
    [InlineData("http://channels.weixin.qq.com/web/pages/feed?oid=1&nid=2")]
    [InlineData("/web/pages/feed?oid=1&nid=2")]
    [InlineData("not a url")]
    public void TryCreate_GivenPageOutsideTheFeedHost_ReturnsFalse(string page)
    {
        string json = $$"""
            {
              "kind": "video",
              "page": "{{page}}",
              "url": "https://finder.video.qq.com/media"
            }
            """;

        Assert.False(WeChatCapturePayload.TryCreate(json, out _));
    }

    [Theory]
    [InlineData("https://example.com/media")]
    [InlineData("http://finder.video.qq.com/media")]
    [InlineData("https://finder.video.qq.com.evil.test/media")]
    public void TryCreate_GivenMediaOutsideTheMediaHost_ReturnsFalse(string url)
    {
        string json = $$"""
            {
              "kind": "video",
              "page": "{{FeedPage}}",
              "url": "{{url}}"
            }
            """;

        Assert.False(WeChatCapturePayload.TryCreate(json, out _));
    }

    [Theory]
    [InlineData("{\"kind\":\"page\"}")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("{")]
    public void TryCreate_GivenNonVideoOrInvalidJson_ReturnsFalse(string json)
    {
        Assert.False(WeChatCapturePayload.TryCreate(json, out _));
    }

    [Fact]
    public void TryCreate_GivenNumericStrings_ParsesNumbers()
    {
        string json = $$"""
            {
              "kind": "video",
              "page": "{{FeedPage}}",
              "url": "https://finder.video.qq.com/media",
              "fileSize": "12345",
              "videoLength": "42"
            }
            """;

        Assert.True(WeChatCapturePayload.TryCreate(json, out WeChatVideoCaptureItem? item));
        Assert.Equal(12_345, item!.ReportedFileSize);
        Assert.Equal(42, item.VideoLengthSeconds);
    }

    [Fact]
    public void TryCreate_GivenInvalidCoverAndMissingFeedIds_LeavesThemNull()
    {
        string json = """
            {
              "kind": "video",
              "page": "https://channels.weixin.qq.com/web/pages/feed",
              "url": "https://finder.video.qq.com/media",
              "thumbUrl": "https://example.com/cover"
            }
            """;

        Assert.True(WeChatCapturePayload.TryCreate(json, out WeChatVideoCaptureItem? item));
        Assert.Null(item!.ThumbUrl);
        Assert.Null(item.Oid);
        Assert.Null(item.Nid);
    }

    [Fact]
    public void TryCreate_GivenEncodedFeedIds_DecodesThem()
    {
        string json = $$"""
            {
              "kind": "video",
              "page": "{{FeedPage}}&name=a%2Bb",
              "url": "https://finder.video.qq.com/media"
            }
            """;

        Assert.True(WeChatCapturePayload.TryCreate(json, out WeChatVideoCaptureItem? item));
        Assert.Equal("0O7-RjlWGoQ", item!.Oid);
        Assert.Equal("HwUEeFRlIP0", item.Nid);
    }
}
