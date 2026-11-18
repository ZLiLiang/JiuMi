using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoUrlTests
{
    [Fact]
    public void CreateDeduplicationKey_GivenOidAndNid_IgnoresTheMediaUrl()
    {
        WeChatVideoCaptureItem first = Capture("https://finder.video.qq.com/a?token=one", "oid-1", "nid-1");
        WeChatVideoCaptureItem second = Capture("https://finder.video.qq.com/b?token=two", "oid-1", "nid-1");

        Assert.Equal(
            WeChatVideoUrl.CreateDeduplicationKey(first),
            WeChatVideoUrl.CreateDeduplicationKey(second));
    }

    [Fact]
    public void CreateDeduplicationKey_GivenMissingNid_FallsBackToUrlWithoutToken()
    {
        WeChatVideoCaptureItem first = Capture("https://finder.video.qq.com/a?token=one&x=1", oid: "oid-1");
        WeChatVideoCaptureItem second = Capture("https://finder.video.qq.com/a?x=1&token=two", oid: "oid-1");

        Assert.Equal(
            WeChatVideoUrl.CreateDeduplicationKey(first),
            WeChatVideoUrl.CreateDeduplicationKey(second));
    }

    [Fact]
    public void CreateDeduplicationKey_GivenDifferentOid_DoesNotMatch()
    {
        WeChatVideoCaptureItem first = Capture("https://finder.video.qq.com/a?token=one", "oid-1", "nid-1");
        WeChatVideoCaptureItem second = Capture("https://finder.video.qq.com/a?token=one", "oid-2", "nid-1");

        Assert.NotEqual(
            WeChatVideoUrl.CreateDeduplicationKey(first),
            WeChatVideoUrl.CreateDeduplicationKey(second));
    }

    [Fact]
    public void RemoveTokenParameter_GivenTokenAmongParameters_KeepsTheOthers()
    {
        string url = WeChatVideoUrl.RemoveTokenParameter(
            "https://finder.video.qq.com/path?token=secret&x=1&token=again&y=2");

        Assert.Equal("https://finder.video.qq.com/path?x=1&y=2", url);
    }

    [Fact]
    public void RemoveTokenParameter_GivenOnlyToken_RemovesTheQuery()
    {
        string url = WeChatVideoUrl.RemoveTokenParameter(
            "https://finder.video.qq.com/path?token=secret");

        Assert.Equal("https://finder.video.qq.com/path", url);
    }

    [Fact]
    public void RemoveTokenParameter_GivenNoToken_ReturnsTheOriginal()
    {
        const string url = "https://finder.video.qq.com/path?x=1";

        Assert.Equal(url, WeChatVideoUrl.RemoveTokenParameter(url));
    }

    [Fact]
    public void RemoveTokenParameter_GivenNoQuery_ReturnsTheOriginal()
    {
        const string url = "https://finder.video.qq.com/path";

        Assert.Equal(url, WeChatVideoUrl.RemoveTokenParameter(url));
    }

    private static WeChatVideoCaptureItem Capture(string url, string? oid = null, string? nid = null) => new()
    {
        Url = url,
        PageUrl = "https://channels.weixin.qq.com/web/pages/feed",
        Oid = oid,
        Nid = nid,
    };
}
