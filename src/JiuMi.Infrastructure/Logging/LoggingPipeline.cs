using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using JiuMi.Application;
using JiuMi.Application.Logging;
using JiuMi.Application.Settings;
using Serilog;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Builds the application logger: a bootstrap sink that buffers until the settings probe decides the
/// log directory, then the file sink the rest of the session uses.
/// </summary>
public sealed class LoggingPipeline : IDisposable
{
    private const long FileSizeLimitBytes = 5L * 1024 * 1024;
    private const int RetainedFileCount = 30;

    private static readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);

    private readonly ForwardingLogSink _forwarder;
    private readonly Serilog.Core.Logger _logger;
    private readonly LoggingStatus _status;

    private Timer? _healthTimer;
    private SettingsLocation? _pendingLocation;
    private string? _fileLogDirectory;
    private bool _hasFileSink;
    private bool _attachFailureReported;
    private int _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingPipeline"/> class.
    /// </summary>
    /// <param name="applicationInfo">Supplies the version attached to every event.</param>
    /// <param name="levelOverride">The raw <c>JIUMI_LOG_LEVEL</c> value, or <see langword="null"/>.</param>
    public LoggingPipeline(IApplicationInfo applicationInfo, string? levelOverride)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);

        LogLevelOverride setting = LogLevelOverride.Parse(levelOverride);
        _status = new LoggingStatus(string.IsNullOrWhiteSpace(levelOverride) ? null : levelOverride);
        Status = _status;
        _forwarder = new ForwardingLogSink(_status);

        LoggerConfiguration configuration = new LoggerConfiguration()
            .MinimumLevel.Is(setting.Level)
            .Enrich.With(new AmbientLogEnricher(applicationInfo.Version))
            .WriteTo.Sink(_forwarder);

#if DEBUG
        configuration = configuration.WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture);
#endif

        _logger = configuration.CreateLogger();
        Logger = _logger;

        // Serilog reports broken sinks through its own channel. The file logger swallows those
        // exceptions by design, so this is the only place a mid-session write failure surfaces.
        Serilog.Debugging.SelfLog.Enable(message =>
        {
            Debug.WriteLine(message);
            if (message.Contains("Caught exception while emitting to sink", StringComparison.Ordinal))
            {
                _status.SetFileLoggingAvailable(false);
            }
        });

        if (setting.RejectedValue is not null)
        {
            _logger.Warning(
                "Invalid {Variable} value {Value}; using Information",
                LogLevelOverride.VariableName,
                setting.RejectedValue);
        }
    }

    /// <summary>
    /// Gets the logger every layer writes through.
    /// </summary>
    public Serilog.ILogger Logger
    {
        get;
    }

    /// <summary>
    /// Gets the observable state shown in the about section.
    /// </summary>
    public ILoggingStatus Status
    {
        get;
    }

    /// <summary>
    /// Creates the file sink at the directory the probe settled on and replays buffered startup events.
    /// </summary>
    /// <param name="location">The effective settings location.</param>
    /// <remarks>
    /// This never throws: a log that cannot be written degrades to the unavailable state and leaves the
    /// application running. The call happens on the startup step that settles the probe, before any
    /// user-visible work depends on it.
    /// </remarks>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Logging is a side channel; any initialization failure must degrade to the unavailable state instead of faulting startup.")]
    public void AttachFileLogging(SettingsLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (Volatile.Read(ref _isDisposed) != 0 || _hasFileSink)
        {
            return;
        }

        string? directory = LogPaths.For(location);
        if (directory is null)
        {
            _pendingLocation = null;
            _status.SetFileLoggingAvailable(false);
            _forwarder.Discard();
            _logger.Warning("File logging is unavailable because no settings directory is writable");
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            // Ownership moves to the forwarding sink, which disposes the file logger.
#pragma warning disable CA2000
            Serilog.Core.Logger fileLogger = CreateFileLogger(directory);
            _forwarder.Attach(new ConfigPathLogSink(fileLogger, location.FilePath));
#pragma warning restore CA2000
            _hasFileSink = true;
            _pendingLocation = null;
            _attachFailureReported = false;
            _fileLogDirectory = directory;
            StartHealthCheck(directory);
            _logger.Debug("File logging started in {LogDirectory}", directory);
        }
        catch (Exception exception)
        {
            _status.SetFileLoggingAvailable(false);
            _forwarder.Discard();
            _pendingLocation = location;
            _fileLogDirectory = directory;
            StartHealthCheck(directory);

            // The health check retries when the directory becomes writable; keep its repeated failures
            // out of the log once the first diagnosis is recorded.
            if (!_attachFailureReported)
            {
                _attachFailureReported = true;
                _logger.Warning(exception, "File logging could not start in {LogDirectory}", directory);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _healthTimer?.Dispose();
        _healthTimer = null;
        Serilog.Debugging.SelfLog.Disable();

        // Closes the file so the directory can be released; the static Log.CloseAndFlush() at exit
        // disposes the outer logger and reaches the same (now idempotent) sink.
        _forwarder.Dispose();
    }

    private void StartHealthCheck(string directory)
    {
        // A write can fail after a successful start (disk removed, permissions changed), or the initial
        // attach can fail and later become possible. The file logger swallows write failures, so the
        // status is the only signal: while it is down, probe the directory and retry the attach.
        _healthTimer ??= new Timer(
            _ => CheckFileLoggingHealth(),
            state: null,
            _healthCheckInterval,
            _healthCheckInterval);
    }

    private void CheckFileLoggingHealth()
    {
        if (Volatile.Read(ref _isDisposed) != 0 ||
            _status.IsFileLoggingAvailable ||
            _fileLogDirectory is not { } directory)
        {
            return;
        }

        if (!CanWriteToLogDirectory(directory))
        {
            return;
        }

        if (!_hasFileSink && _pendingLocation is { } pendingLocation)
        {
            AttachFileLogging(pendingLocation);
            return;
        }

        _status.SetFileLoggingAvailable(true);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The health check is advisory: any failure only means the log directory is still unusable.")]
    private static bool CanWriteToLogDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probePath = Path.Combine(directory, ".logging-probe-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }
            }
            finally
            {
                TryDelete(probePath);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Serilog.Core.Logger CreateFileLogger(string directory)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                new RedactingCompactJsonFormatter(),
                Path.Combine(directory, "jiuMi-.jsonl"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: RetainedFileCount,
                buffered: false,
                shared: false,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .CreateLogger();
    }
}
