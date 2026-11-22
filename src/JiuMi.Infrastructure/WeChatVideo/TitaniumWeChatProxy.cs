using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using JiuMi.Application.WeChatVideo;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Runs Titanium as a loopback MITM proxy that only decrypts the two WeChat hosts and injects the page script.
/// </summary>
internal sealed class TitaniumWeChatProxy : IDisposable
{
    private readonly X509Certificate2 _rootCertificate;
    private readonly string _script;
    private readonly Serilog.ILogger _logger;
    private readonly Action<WeChatMediaCapture>? _onMediaCaptured;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, byte> _capturedMediaUrls = new(StringComparer.Ordinal);
    private ProxyServer? _proxy;
    private ExplicitProxyEndPoint? _endpoint;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitaniumWeChatProxy"/> class.
    /// </summary>
    /// <param name="rootCertificate">The signing authority trusted for the current user.</param>
    /// <param name="receiverUrl">The loopback listener the script posts to.</param>
    /// <param name="logger">Records injection failures without tokens.</param>
    /// <param name="onMediaCaptured">Receives complete video responses, when provided.</param>
    /// <param name="timeProvider">Supplies the media capture time, when provided.</param>
    public TitaniumWeChatProxy(
        X509Certificate2 rootCertificate,
        Uri receiverUrl,
        Serilog.ILogger logger,
        Action<WeChatMediaCapture>? onMediaCaptured = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(rootCertificate);
        ArgumentNullException.ThrowIfNull(receiverUrl);
        ArgumentNullException.ThrowIfNull(logger);
        _rootCertificate = rootCertificate;
        _script = WeChatInjectionScript.Create(receiverUrl);
        _logger = logger;
        _onMediaCaptured = onMediaCaptured;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts the proxy on the given loopback port without touching the system proxy.
    /// </summary>
    /// <param name="port">The loopback port to listen on.</param>
    public void Start(int port)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var proxy = new ProxyServer(userTrustRootCertificate: false);
        try
        {
            proxy.CertificateManager.RootCertificate = _rootCertificate;
            proxy.CertificateManager.SaveFakeCertificates = false;

            // The certificate and its trust are installed explicitly by the capture; Titanium must
            // never open a second interactive root-store dialog on top of that.
            CertificateManager.SuppressInteractiveRootStoreMutations = true;
            proxy.BeforeResponse += OnBeforeResponse;

            var endpoint = new ExplicitProxyEndPoint(IPAddress.Loopback, port, decryptSsl: true);
            endpoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnectRequest;
            proxy.AddEndPoint(endpoint);
            proxy.Start(changeSystemProxySettings: false);

            _proxy = proxy;
            _endpoint = endpoint;
        }
        catch
        {
            proxy.BeforeResponse -= OnBeforeResponse;
            proxy.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Stops the proxy and releases its endpoints.
    /// </summary>
    /// <returns>A task that completes after the proxy has drained.</returns>
    public async Task StopAsync()
    {
        ProxyServer? proxy = _proxy;
        _proxy = null;
        if (proxy is null)
        {
            return;
        }

        proxy.BeforeResponse -= OnBeforeResponse;
        if (_endpoint is not null)
        {
            _endpoint.BeforeTunnelConnectRequest -= OnBeforeTunnelConnectRequest;
            proxy.RemoveEndPoint(_endpoint);
            _endpoint = null;
        }

        try
        {
            await proxy.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "WeChat capture proxy did not stop cleanly");
        }
        finally
        {
            proxy.Dispose();
        }
    }

    /// <summary>
    /// Stops the proxy synchronously for process-exit cleanup.
    /// </summary>
    public void StopSynchronously()
    {
        ProxyServer? proxy = _proxy;
        _proxy = null;
        if (proxy is null)
        {
            return;
        }

        proxy.BeforeResponse -= OnBeforeResponse;
        try
        {
            proxy.Stop();
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "WeChat capture proxy did not stop cleanly during shutdown");
        }
        finally
        {
            proxy.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopSynchronously();
    }

    private static Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
    {
        e.DecryptSsl = WeChatCaptureHosts.ShouldDecrypt(e.HttpClient.Request.RequestUri.Host);
        return Task.CompletedTask;
    }

    private async Task OnBeforeResponse(object sender, SessionEventArgs e)
    {
        Request request = e.HttpClient.Request;
        Response response = e.HttpClient.Response;
        if (_onMediaCaptured is not null &&
            WeChatMediaRequestPolicy.IsVideoResponse(
                request.RequestUri.Host,
                request.RequestUri.AbsolutePath,
                response.StatusCode) &&
            WeChatMediaRequestPolicy.TryGetTotalBytes(
                response.Headers
                    .FirstOrDefault(header => string.Equals(
                        header.Name,
                        "Content-Range",
                        StringComparison.OrdinalIgnoreCase))
                    ?.Value,
                response.ContentLength,
                out long totalBytes))
        {
            string url = request.RequestUri.AbsoluteUri;
            if (_capturedMediaUrls.TryAdd(url, 0))
            {
                try
                {
                    _onMediaCaptured(new WeChatMediaCapture(
                        request.RequestUri,
                        totalBytes,
                        _timeProvider.GetUtcNow()));
                }
                catch (Exception exception)
                {
                    _logger.Warning(exception, "The WeChat media capture callback failed");
                }
            }

            return;
        }

        if (!WeChatCaptureResponsePolicy.ShouldInject(
            request.RequestUri.Host,
            response.StatusCode,
            response.ContentType,
            response.ContentLength))
        {
            return;
        }

        try
        {
            string html = Encoding.UTF8.GetString(await e.GetResponseBody().ConfigureAwait(false));
            string injected = WeChatHtmlInjector.Inject(html, _script);
            if (!string.Equals(injected, html, StringComparison.Ordinal))
            {
                e.SetResponseBody(Encoding.UTF8.GetBytes(injected));
            }

            // A cached page never reaches this hook, so its script would be missing; keep the page
            // fresh for the next open as well.
            response.Headers.RemoveHeader("Cache-Control");
            response.Headers.AddHeader("Cache-Control", "no-store");
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "WeChat capture script injection failed for {Host}", request.RequestUri.Host);
        }
    }
}
