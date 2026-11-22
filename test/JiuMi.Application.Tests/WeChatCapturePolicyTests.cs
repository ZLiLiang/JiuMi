using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatCapturePolicyTests
{
    [Theory]
    [InlineData("channels.weixin.qq.com")]
    [InlineData("CHANNELS.WEIXIN.QQ.COM")]
    [InlineData("finder.video.qq.com")]
    [InlineData("findera4.video.qq.com")]
    [InlineData("findernd.video.qq.com")]
    [InlineData("video.qq.com")]
    public void ShouldDecrypt_GivenCaptureHost_ReturnsTrue(string host)
    {
        Assert.True(WeChatCaptureHosts.ShouldDecrypt(host));
    }

    [Theory]
    [InlineData("weixin.qq.com")]
    [InlineData("channels.weixin.qq.com.evil.test")]
    [InlineData("example.com")]
    [InlineData(null)]
    public void ShouldDecrypt_GivenOtherHost_ReturnsFalse(string? host)
    {
        Assert.False(WeChatCaptureHosts.ShouldDecrypt(host));
    }

    [Fact]
    public void ShouldInject_GivenPageHostHtml200_ReturnsTrue()
    {
        Assert.True(WeChatCaptureResponsePolicy.ShouldInject(
            "channels.weixin.qq.com",
            200,
            "text/html; charset=utf-8",
            1024));
    }

    [Theory]
    [InlineData("finder.video.qq.com", 200, "text/html", 1024)]
    [InlineData("channels.weixin.qq.com", 304, "text/html", 1024)]
    [InlineData("channels.weixin.qq.com", 200, "application/json", 1024)]
    [InlineData("channels.weixin.qq.com", 200, "text/html", WeChatCaptureResponsePolicy.MaxHtmlBytes + 1)]
    [InlineData("channels.weixin.qq.com", 200, null, 1024)]
    public void ShouldInject_GivenAnythingElse_ReturnsFalse(
        string host,
        int statusCode,
        string? contentType,
        long contentLength)
    {
        Assert.False(WeChatCaptureResponsePolicy.ShouldInject(host, statusCode, contentType, contentLength));
    }

    [Fact]
    public void ShouldInject_GivenUnknownLength_ReturnsTrue()
    {
        Assert.True(WeChatCaptureResponsePolicy.ShouldInject(
            "channels.weixin.qq.com",
            200,
            "text/html",
            -1));
    }
}
