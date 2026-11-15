using System.Net;
using System.Net.Http;
using System.Text;
using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatCaptureListenerTests : IDisposable
{
    private const string ValidPayload = """
        {
          "kind": "video",
          "page": "https://channels.weixin.qq.com/web/pages/feed?oid=oid-1&nid=nid-1",
          "url": "https://finder.video.qq.com/251/20302/stodownload?token=abc",
          "decodeKey": "42",
          "description": "description",
          "uploader": "uploader"
        }
        """;

    private readonly HttpClient _client = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Start_GivenTwoListeners_UsesDifferentPortsAndTokens()
    {
        using var first = new WeChatCaptureListener(_ => { }, Logger.None);
        using var second = new WeChatCaptureListener(_ => { }, Logger.None);

        WeChatCaptureListenerStart firstStart = first.Start();
        WeChatCaptureListenerStart secondStart = second.Start();

        Assert.NotEqual(firstStart.Port, secondStart.Port);
        Assert.NotEqual(firstStart.ReceiverUrl.AbsolutePath, secondStart.ReceiverUrl.AbsolutePath);
        Assert.True(firstStart.Port is >= 1 and <= 65535);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_GivenValidFeedVideo_ReturnsNoContentAndRaisesTheItem()
    {
        var captured = new TaskCompletionSource<WeChatVideoCaptureItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new WeChatCaptureListener(item => captured.TrySetResult(item), Logger.None);
        WeChatCaptureListenerStart start = listener.Start();

        using HttpResponseMessage response = await PostAsync(start, ValidPayload);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        WeChatVideoCaptureItem item = await captured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("description", item.Description);
        Assert.Equal("oid-1", item.Oid);
    }

    [Fact]
    public async Task Handle_GivenWrongPath_ReturnsNotFound()
    {
        using var listener = new WeChatCaptureListener(_ => { }, Logger.None);
        WeChatCaptureListenerStart start = listener.Start();
        var wrong = new Uri(start.ReceiverUrl, "/other/");
        using var content = new StringContent(ValidPayload, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _client.PostAsync(wrong, content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Handle_GivenGet_ReturnsMethodNotAllowed()
    {
        using var listener = new WeChatCaptureListener(_ => { }, Logger.None);
        WeChatCaptureListenerStart start = listener.Start();

        using HttpResponseMessage response = await _client.GetAsync(start.ReceiverUrl);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Handle_GivenOversizeBody_ReturnsRequestEntityTooLarge()
    {
        using var listener = new WeChatCaptureListener(_ => { }, Logger.None);
        WeChatCaptureListenerStart start = listener.Start();
        string body = new('x', WeChatCaptureListener.MaxBodyBytes + 1);

        using HttpResponseMessage response = await PostAsync(start, body);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "kind": "video", "page": "https://channels.weixin.qq.com/web/pages/home?oid=1&nid=2", "url": "https://finder.video.qq.com/media" }""")]
    [InlineData("""{ "kind": "page", "page": "https://channels.weixin.qq.com/web/pages/feed?oid=1&nid=2", "url": "https://finder.video.qq.com/media" }""")]
    public async Task Handle_GivenInvalidPayload_ReturnsBadRequest(string body)
    {
        using var listener = new WeChatCaptureListener(_ => { }, Logger.None);
        WeChatCaptureListenerStart start = listener.Start();

        using HttpResponseMessage response = await PostAsync(start, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(WeChatCaptureListenerStart start, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        return await _client.PostAsync(start.ReceiverUrl, content);
    }
}
