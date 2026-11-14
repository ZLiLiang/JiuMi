using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;

internal static class Program
{
    private const string CertName = "JiumiSpike";
    private const string CertPassword = "jiumi-spike";
    private const int MaxCaptureBytes = 1024 * 1024;
    private const int MaxInjectBytes = 4 * 1024 * 1024;

    private static readonly string LogDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    private static readonly string TrafficFile = Path.Combine(LogDir, "traffic.log");
    private static readonly string CaptureFile = Path.Combine(LogDir, "captures.ndjson");
    private static readonly string SnapshotFile = Path.Combine(LogDir, "proxy-snapshot.json");
    private static readonly string StopFile = Path.Combine(LogDir, "STOP");
    private static readonly object FileLock = new();

    private static ProxyServer? _proxy;
    private static ExplicitProxyEndPoint? _endpoint;
    private static TcpListener? _captureListener;
    private static string _captureUrl = string.Empty;
    private static string _capturePath = string.Empty;
    private static string _injectionScript = string.Empty;
    private static ProxySnapshot? _snapshot;
    private static string? _rootThumbprint;
    private static bool _cleaned;
    private static int _captureCount;

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: " + ex);
            Cleanup();
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        int minutes = GetIntArg(args, "--minutes=", 30);
        bool noSystemProxy = args.Contains("--no-system-proxy");
        Directory.CreateDirectory(LogDir);
        File.Delete(StopFile);

        _snapshot = ProxySnapshot.Capture();
        File.WriteAllText(SnapshotFile, JsonSerializer.Serialize(_snapshot, new JsonSerializerOptions { WriteIndented = true }));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
        Console.WriteLine("Proxy snapshot:");
        Console.WriteLine(_snapshot);
        Console.WriteLine($"Logs: {LogDir}");

        StartCaptureListener();
        _injectionScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InjectionScript.js"))
            .Replace("__CAPTURE_URL__", _captureUrl, StringComparison.Ordinal);

        Console.WriteLine("Creating root certificate (a Windows trust prompt may appear; choose Yes)...");
        _proxy = new ProxyServer();
        _proxy.CertificateManager.CertificateEngine = CertificateEngine.DefaultWindows;
        _proxy.CertificateManager.RootCertificateName = CertName;
        _proxy.CertificateManager.RootCertificateIssuerName = CertName;
        _proxy.CertificateManager.PfxFilePath = Path.Combine(LogDir, CertName + ".pfx");
        _proxy.CertificateManager.PfxPassword = CertPassword;
        _proxy.CertificateManager.SaveFakeCertificates = false;
        _proxy.CertificateManager.EnsureRootCertificate(true, false, false);
        _rootThumbprint = _proxy.CertificateManager.RootCertificate?.Thumbprint;
        Console.WriteLine($"Root cert thumbprint: {_rootThumbprint}");

        int proxyPort = GetFreePort();
        _endpoint = new ExplicitProxyEndPoint(IPAddress.Loopback, proxyPort, true);
        _endpoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnect;
        _proxy.BeforeRequest += OnBeforeRequest;
        _proxy.BeforeResponse += OnBeforeResponse;
        _proxy.Start();
        _proxy.AddEndPoint(_endpoint);

        if (!noSystemProxy)
        {
            _proxy.SetAsSystemHttpsProxy(_endpoint);
            Console.WriteLine($"System HTTPS proxy -> 127.0.0.1:{proxyPort}");
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cleanup();
            Environment.Exit(0);
        };

        Console.WriteLine();
        Console.WriteLine("=== WeChat MITM spike READY ===");
        Console.WriteLine($"Proxy:   127.0.0.1:{proxyPort}");
        Console.WriteLine($"Capture: {_captureUrl}");
        Console.WriteLine("1) In WeChat, open a video shared in a chat and play it 5-10 s.");
        Console.WriteLine("2) Open Channels (视频号), open any video and play it 5-10 s.");
        Console.WriteLine("3) Create logs/STOP (or Ctrl+C) to stop and restore the proxy/certificate.");
        Console.WriteLine($"Auto-stop after {minutes} minutes.");
        Console.WriteLine();

        DateTime deadline = DateTime.UtcNow.AddMinutes(minutes);
        while (DateTime.UtcNow < deadline && !File.Exists(StopFile))
        {
            Thread.Sleep(300);
        }

        Cleanup();
        return 0;
    }

    private static void StartCaptureListener()
    {
        _captureListener = new TcpListener(IPAddress.Loopback, 0);
        _captureListener.Start();
        int port = ((IPEndPoint)_captureListener.LocalEndpoint).Port;
        _capturePath = "/capture/" + Guid.NewGuid().ToString("N");
        _captureUrl = $"http://127.0.0.1:{port}{_capturePath}";
        _ = Task.Run(AcceptLoopAsync);
    }

    private static async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _captureListener!.AcceptTcpClientAsync();
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => HandleCaptureAsync(client));
        }
    }

    private static async Task HandleCaptureAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                using NetworkStream stream = client.GetStream();
                byte[] headerBuffer = new byte[64 * 1024];
                int total = 0;
                int headerEnd = -1;
                while (total < headerBuffer.Length)
                {
                    int read = await stream.ReadAsync(headerBuffer.AsMemory(total));
                    if (read <= 0)
                    {
                        break;
                    }

                    total += read;
                    headerEnd = FindHeaderEnd(headerBuffer, total);
                    if (headerEnd >= 0)
                    {
                        break;
                    }
                }

                if (headerEnd < 0)
                {
                    return;
                }

                string headers = Encoding.ASCII.GetString(headerBuffer, 0, headerEnd);
                string requestLine = headers.Split("\r\n")[0];
                string[] parts = requestLine.Split(' ');
                if (parts.Length < 3 || parts[0] != "POST" || !parts[1].StartsWith(_capturePath, StringComparison.Ordinal))
                {
                    await RespondAsync(stream, "HTTP/1.1 404 Not Found\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
                    return;
                }

                int contentLength = 0;
                foreach (string line in headers.Split("\r\n"))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        _ = int.TryParse(line[15..].Trim(), out contentLength);
                    }
                }

                if (contentLength <= 0 || contentLength > MaxCaptureBytes)
                {
                    await RespondAsync(stream, "HTTP/1.1 413 Payload Too Large\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
                    return;
                }

                int already = total - headerEnd - 4;
                byte[] body = new byte[contentLength];
                int copied = Math.Min(already, contentLength);
                if (copied > 0)
                {
                    Array.Copy(headerBuffer, headerEnd + 4, body, 0, copied);
                }

                while (copied < contentLength)
                {
                    int read = await stream.ReadAsync(body.AsMemory(copied));
                    if (read <= 0)
                    {
                        break;
                    }

                    copied += read;
                }

                if (copied == contentLength)
                {
                    RecordCapture(Encoding.UTF8.GetString(body));
                }

                await RespondAsync(stream, "HTTP/1.1 204 No Content\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
            }
            catch (Exception ex)
            {
                LogTraffic("CAPTURE-ERROR " + ex.Message);
            }
        }
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        for (int i = 0; i + 3 < length; i++)
        {
            if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
            {
                return i;
            }
        }

        return -1;
    }

    private static async Task RespondAsync(NetworkStream stream, string response)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static void RecordCapture(string json)
    {
        Interlocked.Increment(ref _captureCount);
        lock (FileLock)
        {
            File.AppendAllText(CaptureFile, json + "\n", Encoding.UTF8);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string kind = root.TryGetProperty("kind", out JsonElement k) ? k.GetString() ?? "?" : "?";
            string page = root.TryGetProperty("page", out JsonElement p) ? Short(p.GetString(), 120) : string.Empty;
            string summary = kind switch
            {
                "video" => $"desc='{Short(Get(root, "description"), 60)}' uploader='{Short(Get(root, "uploader"), 30)}' size={Get(root, "size")} len={Get(root, "videolen")} host={HostOf(Get(root, "url"))}",
                "network" => $"host={HostOf(Get(root, "url"))} status={Get(root, "status")} bytes={Short(Get(root, "body"), 0, true)}",
                "invoke" => $"cmd={Get(root, "cmd")}",
                "bridge-other" => $"cmd={Get(root, "cmd")} snippet={Short(Get(root, "payload"), 100)}",
                "resource" => $"url={Short(Get(root, "url"), 160)}",
                "page" => $"page='{Short(Get(root, "title"), 40)}' url={Short(Get(root, "page"), 160)}",
                _ => Short(json, 160),
            };
            Console.WriteLine($"[capture#{_captureCount}] {kind}: {summary} | from {page}");
        }
        catch
        {
            Console.WriteLine($"[capture#{_captureCount}] raw: {Short(json, 180)}");
        }
    }

    private static string Get(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.ToString() : string.Empty;

    private static string Short(string? value, int max, bool lengthOnly = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (lengthOnly)
        {
            return value.Length.ToString();
        }

        return value.Length <= max ? value : value[..max] + "...";
    }

    private static string HostOf(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return uri.Host;
        }

        return Short(url, 40);
    }

    private static async Task OnBeforeTunnelConnect(object sender, TunnelConnectSessionEventArgs e)
    {
        await Task.Yield();
        string host = e.HttpClient.Request.RequestUri.Host;
        bool decrypt = host.Equals("channels.weixin.qq.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("finder.video.qq.com", StringComparison.OrdinalIgnoreCase);
        e.DecryptSsl = decrypt;
        if (!decrypt && LooksRelevantHost(host))
        {
            LogTraffic($"CONNECT {host} decrypt=false");
        }
    }

    private static bool LooksRelevantHost(string host) =>
        host.Contains("weixin", StringComparison.OrdinalIgnoreCase)
        || host.Contains("finder", StringComparison.OrdinalIgnoreCase)
        || host.Contains("video.qq.com", StringComparison.OrdinalIgnoreCase)
        || host.Contains("tc.qq.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsDecryptedHost(string host) =>
        host.Equals("channels.weixin.qq.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("finder.video.qq.com", StringComparison.OrdinalIgnoreCase);

    private static Task OnBeforeRequest(object sender, SessionEventArgs e)
    {
        Uri uri = e.HttpClient.Request.RequestUri;
        if (IsDecryptedHost(uri.Host))
        {
            string consoleUrl = uri.GetLeftPart(UriPartial.Path) + (string.IsNullOrEmpty(uri.Query) ? string.Empty : "?…");
            LogTraffic($"REQ {e.HttpClient.Request.Method} {consoleUrl} ct={e.HttpClient.Request.ContentType} len={e.HttpClient.Request.ContentLength}");
        }

        return Task.CompletedTask;
    }

    private static async Task OnBeforeResponse(object sender, SessionEventArgs e)
    {
        Uri uri = e.HttpClient.Request.RequestUri;
        if (!IsDecryptedHost(uri.Host))
        {
            return;
        }

        Response response = e.HttpClient.Response;
        string contentType = response.ContentType ?? string.Empty;
        string consoleUrl = uri.GetLeftPart(UriPartial.Path) + (string.IsNullOrEmpty(uri.Query) ? string.Empty : "?…");
        LogTraffic($"RESP {response.StatusCode} {consoleUrl} ct={contentType} len={response.ContentLength}");

        try
        {
            if (uri.Host.Equals("channels.weixin.qq.com", StringComparison.OrdinalIgnoreCase)
                && response.StatusCode == 200
                && contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && response.ContentLength <= MaxInjectBytes)
            {
                string html = Encoding.UTF8.GetString(await e.GetResponseBody());
                if (!html.Contains("__JIUMI_SPIKE__", StringComparison.Ordinal))
                {
                    string tag = "<script>/*__JIUMI_SPIKE__*/\n" + _injectionScript + "\n</script>";
                    int index = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    string modified = index >= 0 ? html.Insert(index, tag) : html + tag;
                    e.SetResponseBody(Encoding.UTF8.GetBytes(modified));
                    LogTraffic($"INJECT {consoleUrl} script={_injectionScript.Length}B");
                }
            }
            else if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && response.ContentLength is > 0 and <= MaxCaptureBytes)
            {
                string body = await e.GetResponseBodyAsString();
                if (LooksMedia(body))
                {
                    string payload = JsonSerializer.Serialize(new
                    {
                        kind = "mitm-json",
                        ts = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        url = uri.ToString(),
                        status = response.StatusCode,
                        body,
                    });
                    RecordCapture(payload);
                }
            }
        }
        catch (Exception ex)
        {
            LogTraffic($"RESP-ERROR {consoleUrl} {ex.Message}");
        }
    }

    private static bool LooksMedia(string text) =>
        text.Contains("decode_key", StringComparison.OrdinalIgnoreCase)
        || text.Contains("url_token", StringComparison.OrdinalIgnoreCase)
        || text.Contains("object_desc", StringComparison.OrdinalIgnoreCase)
        || text.Contains("video_play_len", StringComparison.OrdinalIgnoreCase)
        || text.Contains("finder", StringComparison.OrdinalIgnoreCase);

    private static void LogTraffic(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (FileLock)
        {
            File.AppendAllText(TrafficFile, line + Environment.NewLine, Encoding.UTF8);
        }

        Console.WriteLine(line);
    }

    private static int GetFreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static int GetIntArg(string[] args, string prefix, int fallback)
    {
        string? value = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal));
        return value is not null && int.TryParse(value[prefix.Length..], out int parsed) ? parsed : fallback;
    }

    private static void Cleanup()
    {
        if (_cleaned)
        {
            return;
        }

        _cleaned = true;
        Console.WriteLine();
        Console.WriteLine("Cleaning up...");
        try
        {
            _proxy?.RestoreOriginalProxySettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RestoreOriginalProxySettings failed: {ex.Message}");
        }

        try
        {
            _snapshot?.Restore();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Manual proxy restore failed: {ex.Message}");
        }

        try
        {
            _proxy?.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Proxy stop failed: {ex.Message}");
        }

        try
        {
            if (_endpoint is not null)
            {
                _proxy?.RemoveEndPoint(_endpoint);
            }
        }
        catch
        {
        }

        try
        {
            _proxy?.CertificateManager.RemoveTrustedRootCertificate(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RemoveTrustedRootCertificate failed: {ex.Message}");
        }

        if (!string.IsNullOrEmpty(_rootThumbprint))
        {
            RemoveFromStore(StoreName.Root, _rootThumbprint);
            RemoveFromStore(StoreName.My, _rootThumbprint);
        }

        try
        {
            _captureListener?.Stop();
        }
        catch
        {
        }

        try
        {
            string pfx = Path.Combine(LogDir, CertName + ".pfx");
            if (File.Exists(pfx))
            {
                File.Delete(pfx);
            }
        }
        catch
        {
        }

        Console.WriteLine("Proxy restored and certificate removed.");
        Console.WriteLine($"Captures: {_captureCount}; logs: {LogDir}");
    }

    private static void RemoveFromStore(StoreName name, string thumbprint)
    {
        try
        {
            using var store = new X509Store(name, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            X509Certificate2Collection matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            foreach (X509Certificate2 cert in matches)
            {
                store.Remove(cert);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RemoveFromStore({name}) failed: {ex.Message}");
        }
    }

    private sealed record ProxySnapshot(
        int? ProxyEnable,
        string? ProxyServer,
        string? ProxyOverride,
        string? AutoConfigURL,
        int? AutoDetect)
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public static ProxySnapshot Capture()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return new ProxySnapshot(
                key?.GetValue("ProxyEnable") as int?,
                key?.GetValue("ProxyServer") as string,
                key?.GetValue("ProxyOverride") as string,
                key?.GetValue("AutoConfigURL") as string,
                key?.GetValue("AutoDetect") as int?);
        }

        public void Restore()
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath)!;
            RestoreValue(key, "ProxyEnable", ProxyEnable);
            RestoreValue(key, "ProxyServer", ProxyServer);
            RestoreValue(key, "ProxyOverride", ProxyOverride);
            RestoreValue(key, "AutoConfigURL", AutoConfigURL);
            RestoreValue(key, "AutoDetect", AutoDetect);
            InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
        }

        private static void RestoreValue(RegistryKey key, string name, object? value)
        {
            if (value is null)
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(name, value, value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
            }
        }

        public override string ToString() =>
            $"ProxyEnable={ProxyEnable} ProxyServer={ProxyServer} ProxyOverride={ProxyOverride} AutoConfigURL={AutoConfigURL} AutoDetect={AutoDetect}";

        [System.Runtime.InteropServices.DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int bufferLength);
    }
}