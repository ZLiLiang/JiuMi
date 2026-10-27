using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Buffers startup events until the settings probe decides where the log file lives, then replays them.
/// </summary>
internal sealed class ForwardingLogSink : ILogEventSink, IDisposable
{
    private const int BufferCapacity = 256;

    private readonly Lock _gate = new();
    private readonly List<LogEvent> _buffer = [];
    private readonly LoggingStatus _status;

    private ILogEventSink? _fileSink;
    private bool _isDiscarded;
    private bool _isDisposed;

    public ForwardingLogSink(LoggingStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        ILogEventSink? sink;
        lock (_gate)
        {
            if (_fileSink is null)
            {
                if (!_isDiscarded)
                {
                    if (_buffer.Count == BufferCapacity)
                    {
                        _buffer.RemoveAt(0);
                    }

                    _buffer.Add(logEvent);
                }

                return;
            }

            sink = _fileSink;
        }

        Write(sink, logEvent);
    }

    public void Attach(ILogEventSink fileSink)
    {
        ArgumentNullException.ThrowIfNull(fileSink);

        bool failed = false;
        lock (_gate)
        {
            _fileSink = fileSink;
            foreach (LogEvent buffered in _buffer)
            {
                try
                {
                    fileSink.Emit(buffered);
                }
#pragma warning disable CA1031 // One unusable sink must not stop the remaining buffered events.
                catch (Exception)
#pragma warning restore CA1031
                {
                    failed = true;
                }
            }

            _buffer.Clear();
        }

        _status.SetFileLoggingAvailable(!failed);
    }

    /// <summary>
    /// Drops events that were waiting for a file sink, for the memory-only and failed-start cases.
    /// </summary>
    public void Discard()
    {
        lock (_gate)
        {
            _isDiscarded = true;
            _buffer.Clear();
        }
    }

    public void Dispose()
    {
        ILogEventSink? sink;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isDiscarded = true;
            _buffer.Clear();
            sink = _fileSink;
            _fileSink = null;
        }

        (sink as IDisposable)?.Dispose();
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "File logging is a side channel: a failing write must only flip the status, never reach a feature path.")]
    private void Write(ILogEventSink sink, LogEvent logEvent)
    {
        try
        {
            sink.Emit(logEvent);
        }
        catch (Exception)
        {
            // The file logger normally swallows its own failures and reports them through SelfLog,
            // which owns the status transition; this catch covers a sink that throws directly.
            _status.SetFileLoggingAvailable(false);
        }
    }
}
