using System;
using System.Text;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Decodes downloaded catalog text, tolerating a byte-order mark.
/// </summary>
internal static class WorldClockCatalogText
{
    public static string Decode(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        ReadOnlySpan<byte> span = content;
        if (span.StartsWith(Encoding.UTF8.Preamble))
        {
            span = span[Encoding.UTF8.Preamble.Length..];
        }

        return Encoding.UTF8.GetString(span);
    }
}
