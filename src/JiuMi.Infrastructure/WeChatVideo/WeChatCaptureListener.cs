using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Accepts the injected page's capture POSTs on a random loopback port and path.
/// </summary>
internal sealed class WeChatCaptureListener : IDisposable
{
    /// <summary>The largest accepted request body.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    private readonly Action<WeChatVideoCaptureItem> _onItem;
    private readonly Serilog.ILogger _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private HttpListener? _listener;
    private string? _path;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatCaptureListener"/> class.
    /// </summary>
    /// <param name="onItem">Receives every valid captured item.</param>
    /// <param name="logger">Records protocol failures at debug level.</param>
    public WeChatCaptureListener(Action<WeChatVideoCaptureItem> onItem, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(onItem);
        ArgumentNullException.ThrowIfNull(logger);
        _onItem = onItem;
        _logger = logger;
    }

    /// <summary>
    /// Starts the listener on a random loopback port and returns the one-time receiver URL.
    /// </summary>
    /// <returns>The port and URL the injected script must post to.</returns>
    public WeChatCaptureListenerStart Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            int port = LoopbackPortAllocator.Allocate();
            string token = RandomNumberGenerator.GetHexString(32, lowercase: true);
            var listener = new HttpListener();
            string path = "/" + token + "/";
            listener.Prefixes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"http://127.0.0.1:{port}{path}"));

            try
            {
                listener.Start();
            }
            catch (HttpListenerException) when (attempt < 4)
            {
                listener.Close();
                continue;
            }

            _listener = listener;
            _path = path;
            _ = Task.Run(() => AcceptLoopAsync(listener, _cancellation.Token));
            return new WeChatCaptureListenerStart(
                port,
                new Uri(string.Create(
                    CultureInfo.InvariantCulture,
                    $"http://127.0.0.1:{port}{path}")));
        }

        throw new InvalidOperationException("No loopback port could be reserved for the WeChat capture listener.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _listener?.Close();
        _listener = null;
        _cancellation.Dispose();
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            _ = HandleAsync(context, cancellationToken);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The loopback listener is a process boundary; one bad request must not fault the server loop.")]
    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(context.Request.Url?.AbsolutePath, _path, StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.Ordinal))
            {
                context.Response.Headers.Add("Allow", "POST");
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (context.Request.ContentLength64 > MaxBodyBytes)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            byte[]? body = await ReadBodyAsync(context.Request.InputStream, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            if (!WeChatCapturePayload.TryCreate(Encoding.UTF8.GetString(body), out WeChatVideoCaptureItem? item) ||
                item is null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            _onItem(item);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (Exception exception) when (exception is IOException or HttpListenerException or ObjectDisposedException)
        {
            _logger.Debug(exception, "WeChat capture request failed");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Debug(exception, "WeChat capture request failed");
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (HttpListenerException)
            {
            }
        }
    }

    private static async Task<byte[]?> ReadBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

/// <summary>
/// Where the listener accepts captures for this session.
/// </summary>
/// <param name="Port">The loopback port.</param>
/// <param name="ReceiverUrl">The URL including the random path token.</param>
internal sealed record WeChatCaptureListenerStart(int Port, Uri ReceiverUrl);
