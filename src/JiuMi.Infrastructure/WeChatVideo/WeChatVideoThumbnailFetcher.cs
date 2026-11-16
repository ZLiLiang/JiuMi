using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Fetches WeChat video covers with the same media request headers as a video download.
/// </summary>
public sealed class WeChatVideoThumbnailFetcher : IWeChatVideoThumbnailFetcher, IDisposable
{
    private const string ImageAccept =
        "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8";

    private readonly HttpClient _client;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoThumbnailFetcher"/> class.
    /// </summary>
    /// <param name="logger">Records failures.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "HttpClient is created with disposeHandler: true and owns the handler.")]
    public WeChatVideoThumbnailFetcher(Serilog.ILogger logger)
        : this(logger, new HttpClientHandler { UseProxy = false })
    {
    }

    internal WeChatVideoThumbnailFetcher(Serilog.ILogger logger, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(handler);

        _logger = logger;
        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    /// <inheritdoc />
    public async Task<WeChatVideoThumbnailResult> FetchAsync(
        Uri thumbnailUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(thumbnailUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, thumbnailUrl);
            WeChatMediaRequest.Apply(request, ImageAccept);
            using HttpResponseMessage response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Debug(
                    "WeChat thumbnail request returned {StatusCode}",
                    (int)response.StatusCode);
                return WeChatVideoThumbnailResult.Placeholder();
            }

            byte[] bytes = await response.Content
                .ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return bytes.Length == 0
                ? WeChatVideoThumbnailResult.Placeholder()
                : WeChatVideoThumbnailResult.Success(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _logger.Debug(
                "WeChat thumbnail request timed out ({ErrorType})",
                exception.GetType().Name);
            return WeChatVideoThumbnailResult.Placeholder();
        }
        catch (HttpRequestException exception)
        {
            _logger.Debug(
                "WeChat thumbnail request failed ({ErrorType})",
                exception.GetType().Name);
            return WeChatVideoThumbnailResult.Placeholder();
        }
        catch (IOException exception)
        {
            _logger.Debug(
                "WeChat thumbnail response could not be read ({ErrorType})",
                exception.GetType().Name);
            return WeChatVideoThumbnailResult.Placeholder();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
