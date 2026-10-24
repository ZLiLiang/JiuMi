namespace JiuMi.Application.Errors;

/// <summary>
/// Accepts error records from any layer and exposes the session's detail buffer.
/// </summary>
public interface IErrorReporter
{
    /// <summary>
    /// Gets the buffer that is the sole source for merge counts and copied details.
    /// </summary>
    ErrorDetailBuffer Details
    {
        get;
    }

    /// <summary>
    /// Raised after a record is stored, with the merged session count.
    /// </summary>
    event EventHandler<ErrorRecord>? Reported;

    /// <summary>
    /// Stores a record and notifies listeners.
    /// </summary>
    /// <param name="record">The reported failure.</param>
    void Report(ErrorRecord record);
}
