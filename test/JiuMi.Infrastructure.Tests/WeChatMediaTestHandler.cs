using System.Net.Http;

namespace JiuMi.Infrastructure.Tests;

internal sealed class WeChatMediaTestHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly List<WeChatMediaTestRequest> _requests = [];

    public WeChatMediaTestHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public WeChatMediaTestRequest[] Requests
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
        var record = new WeChatMediaTestRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.UserAgent.ToString(),
            request.Headers.Referrer?.AbsoluteUri,
            request.Headers.TryGetValues("Origin", out IEnumerable<string>? origins)
                ? origins.FirstOrDefault()
                : null,
            request.Headers.Range?.ToString());
        lock (_requests)
        {
            _requests.Add(record);
        }

        return await _responder(request, cancellationToken);
    }
}

internal sealed record WeChatMediaTestRequest(
    HttpMethod Method,
    Uri Uri,
    string UserAgent,
    string? Referer,
    string? Origin,
    string? Range);
