namespace JiuMi.Application.Errors;

/// <summary>
/// One failure the user may need to know about, with the details shown by the copy action.
/// </summary>
public sealed record ErrorRecord
{
    /// <summary>
    /// Gets the local time at which the failure was recorded.
    /// </summary>
    public required DateTimeOffset Timestamp
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the severity that drives how the notice behaves.
    /// </summary>
    public required ErrorLevel Level
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the stable event code, such as <c>Settings.SaveFailed</c>.
    /// </summary>
    public required string Code
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the operation that failed, such as <c>Settings.Save</c>.
    /// </summary>
    public required string Operation
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the exception type name, when the failure came from an exception.
    /// </summary>
    public string? ExceptionType
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the exception message, when the failure came from an exception.
    /// </summary>
    public string? ExceptionMessage
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the exception stack trace exactly as captured.
    /// </summary>
    public string? StackTrace
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the application version at the time of the failure.
    /// </summary>
    public string? AppVersion
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the operating system description at the time of the failure.
    /// </summary>
    public string? OsDescription
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the effective settings file path, or <see langword="null"/> in memory-only mode.
    /// </summary>
    public string? ConfigPath
    {
        get;
        init;
    }

    /// <summary>
    /// Gets how many times this event code was recorded in the session.
    /// </summary>
    public int Count
    {
        get;
        init;
    } = 1;
}
