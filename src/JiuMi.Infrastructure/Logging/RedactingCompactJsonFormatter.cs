using System.Globalization;
using JiuMi.Application.Errors;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Writes compact JSON lines with the signed-in user's profile path redacted.
/// </summary>
internal sealed class RedactingCompactJsonFormatter : ITextFormatter
{
    private readonly CompactJsonFormatter _inner = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        // Redacting the finished line covers the message, exception text and every property in one
        // place; a per-property enricher would miss values interpolated into the template. JSON escapes
        // backslashes, so the JSON-aware overload knows both forms of the profile path.
        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        _inner.Format(logEvent, buffer);
        output.Write(ErrorRedactor.RedactJson(buffer.ToString()));
    }
}
