using JiuMi.Application.Errors;

namespace JiuMi.Infrastructure.Errors;

/// <summary>
/// Stores reported failures in memory, writes the warning-and-above ones to the log, and raises an
/// in-process event for the UI.
/// </summary>
public sealed class ErrorReporter : IErrorReporter
{
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorReporter"/> class.
    /// </summary>
    /// <param name="logger">Receives one event for each reported failure.</param>
    public ErrorReporter(Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public ErrorDetailBuffer Details
    {
        get;
    } = new();

    /// <inheritdoc />
    public event EventHandler<ErrorRecord>? Reported;

    /// <inheritdoc />
    public void Report(ErrorRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        ErrorRecord stored = Details.Add(record);
        WriteLog(stored);
        Reported?.Invoke(this, stored);
    }

    private void WriteLog(ErrorRecord record)
    {
        Serilog.ILogger logger = _logger
            .ForContext("code", record.Code)
            .ForContext("operation", record.Operation)
            .ForContext("count", record.Count);

        if (!string.IsNullOrEmpty(record.ExceptionType))
        {
            logger = logger.ForContext("exceptionType", record.ExceptionType);
        }

        if (!string.IsNullOrEmpty(record.ExceptionMessage))
        {
            logger = logger.ForContext("exceptionMessage", record.ExceptionMessage);
        }

        if (!string.IsNullOrEmpty(record.StackTrace))
        {
            logger = logger.ForContext("stackTrace", record.StackTrace);
        }

        if (record.Level == ErrorLevel.Warning)
        {
            logger.Warning("Reported a {Level} event", record.Level);
            return;
        }

        logger.Error("Reported an {Level} event", record.Level);
    }
}
