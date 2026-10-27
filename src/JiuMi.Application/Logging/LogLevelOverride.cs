using Serilog.Events;

namespace JiuMi.Application.Logging;

/// <summary>
/// The level the file log runs at after applying the environment override.
/// </summary>
/// <param name="Level">The level to apply.</param>
/// <param name="RejectedValue">The raw override that was rejected, or <see langword="null"/> when none was.</param>
public sealed record LogLevelOverride(LogEventLevel Level, string? RejectedValue)
{
    /// <summary>
    /// The environment variable that can override the release default.
    /// </summary>
    public const string VariableName = "JIUMI_LOG_LEVEL";

    /// <summary>
    /// Parses the override value, falling back to <see cref="LogEventLevel.Information"/>.
    /// </summary>
    /// <param name="value">The environment variable value, or <see langword="null"/> when unset.</param>
    /// <returns>The level to apply and the rejected raw value, when the input was not one of the five levels.</returns>
    /// <remarks>
    /// <c>Verbose</c> is rejected on purpose: the shell never produces it, and ADR-0004 fixes the set at
    /// five levels.
    /// </remarks>
    public static LogLevelOverride Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(LogEventLevel.Information, null);
        }

        string trimmed = value.Trim();
        LogEventLevel level = trimmed.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };

        return string.Equals(trimmed, level.ToString(), StringComparison.OrdinalIgnoreCase)
            ? new(level, null)
            : new(LogEventLevel.Information, trimmed);
    }
}
