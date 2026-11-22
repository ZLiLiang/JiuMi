using System.Globalization;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Builds the PAC that sends only the WeChat page host to the local capture proxy.
/// </summary>
internal static class WeChatPacScript
{
    /// <summary>
    /// Builds a PAC script with the page host on the capture proxy and every other host on the
    /// user's existing proxy (or direct when there is none).
    /// </summary>
    /// <param name="proxyEnable">The existing <c>ProxyEnable</c> value.</param>
    /// <param name="proxyServer">The existing <c>ProxyServer</c> value.</param>
    /// <param name="capturePort">The local capture proxy port.</param>
    /// <returns>The PAC script body.</returns>
    public static string Build(int? proxyEnable, string? proxyServer, int capturePort)
    {
        if (capturePort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(capturePort));
        }

        string fallback = BuildFallback(proxyEnable, proxyServer);
        return $$"""
            function FindProxyForURL(url, host) {
              host = host.toLowerCase();
              if (host === '{{WeChatCaptureHosts.PageHost}}' ||
                  host === 'video.qq.com' ||
                  shExpMatch(host, '*.video.qq.com')) {
                return 'PROXY 127.0.0.1:{{capturePort.ToString(CultureInfo.InvariantCulture)}}';
              }
              {{fallback}}
            }
            """;
    }

    private static string BuildFallback(int? proxyEnable, string? proxyServer)
    {
        if (proxyEnable != 1 || string.IsNullOrWhiteSpace(proxyServer))
        {
            return "return 'DIRECT';";
        }

        string server = proxyServer.Trim();
        if (!server.Contains('=', StringComparison.Ordinal))
        {
            return IsSafeProxyToken(server)
                ? $"return 'PROXY {server}';"
                : "return 'DIRECT';";
        }

        string? http = FindEntry(server, "http");
        string? https = FindEntry(server, "https");
        string httpRule = http is null ? "return 'DIRECT';" : $"return 'PROXY {http}';";
        string httpsRule = https is null ? "return 'DIRECT';" : $"return 'PROXY {https}';";
        return $$"""
            if (url.substring(0, 5) === 'http:') {
                {{httpRule}}
              }
              {{httpsRule}}
            """;
    }

    private static string? FindEntry(string proxyServer, string protocol)
    {
        foreach (string raw in proxyServer.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string entry = raw.Trim();
            int separator = entry.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 ||
                !string.Equals(entry[..separator].Trim(), protocol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = entry[(separator + 1)..].Trim();
            return IsSafeProxyToken(value) ? value : null;
        }

        return null;
    }

    private static bool IsSafeProxyToken(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character) &&
                character is not ('.' or ':' or '-' or '[' or ']'))
            {
                return false;
            }
        }

        return true;
    }
}
