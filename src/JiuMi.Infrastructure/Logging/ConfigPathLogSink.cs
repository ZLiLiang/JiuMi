using Serilog.Core;
using Serilog.Events;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Adds the effective config path to each event before the file logger writes it.
/// </summary>
/// <remarks>
/// The path is only known after the startup probe, so it cannot come from a logger-level enricher:
/// that would settle the probe before the splash can cover it. Buffered startup events therefore pick
/// up the path at attach time, which is exactly when it becomes known.
/// </remarks>
internal sealed class ConfigPathLogSink : ILogEventSink, IDisposable
{
    private readonly Serilog.ILogger _fileLogger;
    private readonly string? _configPath;
    private bool _isDisposed;

    public ConfigPathLogSink(Serilog.ILogger fileLogger, string? configPath)
    {
        ArgumentNullException.ThrowIfNull(fileLogger);
        _fileLogger = fileLogger;
        _configPath = configPath;
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (!string.IsNullOrWhiteSpace(_configPath))
        {
            logEvent.AddPropertyIfAbsent(new LogEventProperty("configPath", new ScalarValue(_configPath)));
        }

        _fileLogger.Write(logEvent);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        (_fileLogger as IDisposable)?.Dispose();
    }
}
