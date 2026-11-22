using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatMediaRequestPolicyTests
{
    [Theory]
    [InlineData("finder.video.qq.com", "/251/20302/stodownload", 206, true)]
    [InlineData("findera4.video.qq.com", "/251/20302/stodownload", 200, true)]
    [InlineData("finder.video.qq.com", "/251/20304/stodownload", 206, false)]
    [InlineData("example.com", "/251/20302/stodownload", 206, false)]
    [InlineData("finder.video.qq.com", "/251/20302/stodownload", 304, false)]
    public void IsVideoResponse_GivenResponse_ChecksTheVideoPath(
        string host,
        string path,
        int statusCode,
        bool expected)
    {
        Assert.Equal(expected, WeChatMediaRequestPolicy.IsVideoResponse(host, path, statusCode));
    }

    [Theory]
    [InlineData("bytes 0-131071/1027964", 131072, 1027964)]
    [InlineData("bytes 0-0/236107", 1, 236107)]
    [InlineData(null, 370351, 370351)]
    [InlineData("", 370351, 370351)]
    public void TryGetTotalBytes_GivenResponse_ReadsTheFullLength(
        string? contentRange,
        long contentLength,
        long expected)
    {
        Assert.True(WeChatMediaRequestPolicy.TryGetTotalBytes(
            contentRange,
            contentLength,
            out long total));
        Assert.Equal(expected, total);
    }

    [Theory]
    [InlineData("bytes 0-0/*", 1)]
    [InlineData("", 0)]
    public void TryGetTotalBytes_GivenUnknownLength_ReturnsFalse(
        string contentRange,
        long contentLength)
    {
        Assert.False(WeChatMediaRequestPolicy.TryGetTotalBytes(
            contentRange,
            contentLength,
            out _));
    }
}
