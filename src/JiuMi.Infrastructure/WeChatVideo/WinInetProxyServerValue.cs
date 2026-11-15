using System.Globalization;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Builds the WinINet <c>ProxyServer</c> value that leaves every other protocol on the user's proxy.
/// </summary>
internal static class WinInetProxyServerValue
{
    /// <summary>
    /// Replaces only the HTTPS entry with the local capture proxy.
    /// </summary>
    /// <param name="existing">The current <c>ProxyServer</c> value.</param>
    /// <param name="port">The local capture proxy port.</param>
    /// <returns>A protocol-specific proxy list whose HTTPS entry points at the local proxy.</returns>
    public static string ForHttps(string? existing, int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        string local = $"https=127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}";
        string value = existing?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return local;
        }

        var entries = new List<string>();
        bool hasProtocolEntry = false;
        foreach (string rawEntry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string entry = rawEntry.Trim();
            if (entry.Length == 0)
            {
                continue;
            }

            int separator = entry.IndexOf('=', StringComparison.Ordinal);
            if (separator >= 0)
            {
                hasProtocolEntry = true;
                string name = entry[..separator].Trim();
                if (string.Equals(name, "https", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            entries.Add(entry);
        }

        if (!hasProtocolEntry)
        {
            entries.Clear();
            entries.Add("http=" + value);
        }

        entries.Add(local);
        return string.Join(';', entries);
    }
}
