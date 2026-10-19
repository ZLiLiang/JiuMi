using System.Runtime.InteropServices;
using JiuMi.Application.Settings;

namespace JiuMi.Application.Errors;

/// <summary>
/// Creates error records with the environment fields shared by every report.
/// </summary>
public sealed class ErrorRecordFactory
{
    private readonly IApplicationInfo _applicationInfo;
    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorRecordFactory"/> class.
    /// </summary>
    /// <param name="applicationInfo">Supplies the application version.</param>
    /// <param name="settingsStore">Supplies the effective settings path.</param>
    /// <param name="timeProvider">Supplies the timestamp.</param>
    public ErrorRecordFactory(
        IApplicationInfo applicationInfo,
        ISettingsStore settingsStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _applicationInfo = applicationInfo;
        _settingsStore = settingsStore;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a record for a failure.
    /// </summary>
    /// <param name="level">The severity of the failure.</param>
    /// <param name="code">The stable event code.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="error">The exception behind the failure, when there is one.</param>
    /// <returns>The record with the environment fields filled in.</returns>
    public ErrorRecord Create(
        ErrorLevel level,
        string code,
        string operation,
        Exception? error = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        return new ErrorRecord
        {
            Timestamp = _timeProvider.GetLocalNow(),
            Level = level,
            Code = code,
            Operation = operation,
            ExceptionType = error?.GetType().FullName,
            ExceptionMessage = error?.Message,
            StackTrace = error?.StackTrace,
            AppVersion = _applicationInfo.Version,
            OsDescription = RuntimeInformation.OSDescription,
            ConfigPath = _settingsStore.Location.FilePath,
        };
    }
}
