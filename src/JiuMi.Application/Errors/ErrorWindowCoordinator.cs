namespace JiuMi.Application.Errors;

/// <summary>
/// Guarantees at most one error window per process and builds what that window shows.
/// </summary>
public sealed class ErrorWindowCoordinator
{
    /// <summary>
    /// Gets the request the open window is showing, or <see langword="null"/> when no window is open.
    /// </summary>
    public ErrorWindowRequest? Current
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets a value indicating whether an error window is already showing.
    /// </summary>
    public bool IsOpen => Current is not null;

    /// <summary>
    /// Opens the request for a failure, or refuses when a window is already showing.
    /// </summary>
    /// <param name="record">The recorded failure.</param>
    /// <param name="canContinue">Whether the main window is usable, which allows the continue action.</param>
    /// <param name="message">The user-facing text that starts the copied details.</param>
    /// <returns>
    /// The request to show, or <see langword="null"/> when a window is already open; the caller then
    /// leaves the process instead of stacking a second window.
    /// </returns>
    public ErrorWindowRequest? Open(ErrorRecord record, bool canContinue, string message)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(message);

        if (Current is not null)
        {
            return null;
        }

        Current = new ErrorWindowRequest(record, canContinue, ErrorRecordFormatter.Format(record, message));
        return Current;
    }

    /// <summary>
    /// Marks the window as closed so a later failure can show one again.
    /// </summary>
    public void Close() => Current = null;
}
