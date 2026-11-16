using System.Net;
using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatVideoThumbnailFetcherTests
{
    private static readonly Uri _url = new("https://finder.video.qq.com/thumb?id=1");

    [Fact]
    public async Task FetchAsync_GivenImage_ReturnsBytesAndMediaHeaders()
    {
        byte[] image = [1, 2, 3, 4];
        using var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(image),
            }));
        using var fetcher = new WeChatVideoThumbnailFetcher(Logger.None, handler);

        WeChatVideoThumbnailResult result = await fetcher.FetchAsync(_url);

        Assert.False(result.IsPlaceholder);
        Assert.Equal(image, result.Bytes);
        RequestRecord request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(_url, request.Uri);
        Assert.Contains("Mozilla/5.0", request.UserAgent, StringComparison.Ordinal);
        Assert.Equal("https://channels.weixin.qq.com/", request.Referer);
        Assert.Equal("https://channels.weixin.qq.com", request.Origin);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FetchAsync_GivenServerError_ReturnsPlaceholder(HttpStatusCode status)
    {
        using var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(status)));
        using var fetcher = new WeChatVideoThumbnailFetcher(Logger.None, handler);

        WeChatVideoThumbnailResult result = await fetcher.FetchAsync(_url);

        Assert.True(result.IsPlaceholder);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task FetchAsync_GivenNetworkError_ReturnsPlaceholder()
    {
        using var handler = new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        using var fetcher = new WeChatVideoThumbnailFetcher(Logger.None, handler);

        WeChatVideoThumbnailResult result = await fetcher.FetchAsync(_url);

        Assert.True(result.IsPlaceholder);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task FetchAsync_GivenCancellation_Propagates()
    {
        using var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The request should have been canceled.");
        });
        using var fetcher = new WeChatVideoThumbnailFetcher(Logger.None, handler);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fetcher.FetchAsync(_url, source.Token));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        private readonly List<RequestRecord> _requests = [];

        public RecordingHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public RequestRecord[] Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToArray();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var record = new RequestRecord(
                request.Method,
                request.RequestUri!,
                request.Headers.UserAgent.ToString(),
                request.Headers.Referrer?.AbsoluteUri,
                request.Headers.TryGetValues("Origin", out IEnumerable<string>? origins)
                    ? origins.FirstOrDefault()
                    : null);
            lock (_requests)
            {
                _requests.Add(record);
            }

            return await _responder(request, cancellationToken);
        }
    }

    private sealed record RequestRecord(
        HttpMethod Method,
        Uri Uri,
        string UserAgent,
        string? Referer,
        string? Origin);
}
