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

    [Fact]
    public void CreateDeduplicationKey_GivenEncFileKey_IgnoresSubdomainTokenAndDecodeKey()
    {
        const string encKey = "1234567890abcdef1234567890abcdef9999";
        WeChatVideoCaptureItem first = Capture(
            $"https://finder.video.qq.com/251/20302/stodownload?encfilekey={encKey}&token=one",
            oid: "oid-1",
            nid: "nid-1",
            decodeKey: "111111");
        WeChatVideoCaptureItem second = Capture(
            $"https://findera4.video.qq.com/251/20302/stodownload?encfilekey={encKey}&token=two",
            oid: "oid-2",
            nid: "nid-2",
            decodeKey: "222222");

        Assert.Equal(
            WeChatVideoUrl.CreateDeduplicationKey(first),
            WeChatVideoUrl.CreateDeduplicationKey(second));
    }

    [Fact]
    public void CreateDeduplicationKey_GivenDifferentEncFileKey_DoesNotMatch()
    {
        WeChatVideoCaptureItem first = Capture(
            "https://finder.video.qq.com/251/20302/stodownload?encfilekey=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1111&token=one");
        WeChatVideoCaptureItem second = Capture(
            "https://finder.video.qq.com/251/20302/stodownload?encfilekey=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2222&token=one");

        Assert.NotEqual(
            WeChatVideoUrl.CreateDeduplicationKey(first),
            WeChatVideoUrl.CreateDeduplicationKey(second));
    }

    [Theory]
    [InlineData("finder.video.qq.com", true)]
    [InlineData("findera4.video.qq.com", true)]
    [InlineData("findernd.video.qq.com", true)]
    [InlineData("FINDERMP.VIDEO.QQ.COM", true)]
    [InlineData("video.qq.com", false)]
    [InlineData("finder.video.qq.com.evil.example", false)]
    [InlineData("channels.weixin.qq.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedMediaHost_GivenHost_MatchesVideoQqSubdomainsOnly(string? host, bool expected)
    {
        Assert.Equal(expected, WeChatVideoUrl.IsAllowedMediaHost(host));
    }

    [Theory]
    [InlineData("https://finder.video.qq.com/251/20302/stodownload?encfilekey=abc", true)]
    [InlineData("https://findera4.video.qq.com/251/20304/stodownload?encfilekey=abc", true)]
    [InlineData("https://findernd.video.qq.com/cover.jpg", true)]
    [InlineData("http://finder.video.qq.com/251/20302/stodownload?encfilekey=abc", false)]
    [InlineData("https://example.com/251/20302/stodownload?encfilekey=abc", false)]
    [InlineData("not-a-url", false)]
    [InlineData(null, false)]
    public void IsAllowedMediaUrl_GivenUrl_RequiresHttpsVideoQqSubdomain(string? url, bool expected)
    {
        Assert.Equal(expected, WeChatVideoUrl.IsAllowedMediaUrl(url));
    }

    [Fact]
    public void TryExtractEncFileKeyPrefix_GivenValidUrl_ReturnsFirst32Chars()
    {
        const string encKey = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEF";
        string url = $"https://findernd.video.qq.com/251/20302/stodownload?encfilekey={encKey}&token=abc";

        bool ok = WeChatVideoUrl.TryExtractEncFileKeyPrefix(url, out string prefix);

        Assert.True(ok);
        Assert.Equal(encKey[..32], prefix);
    }

    private static WeChatVideoCaptureItem Capture(
        string url,
        string? oid = null,
        string? nid = null,
        string? decodeKey = null) => new()
        {
            Url = url,
            PageUrl = "https://channels.weixin.qq.com/web/pages/feed",
            Oid = oid,
            Nid = nid,
            DecodeKey = decodeKey,
        };
}
