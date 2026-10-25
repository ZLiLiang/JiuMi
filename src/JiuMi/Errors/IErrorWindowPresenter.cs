namespace JiuMi.Errors;

/// <summary>
/// Turns failures that cannot be handled in place into the user-visible error window.
/// </summary>
public interface IErrorWindowPresenter
{
    /// <summary>
    /// Presents an exception that escaped the UI thread dispatcher.
    /// </summary>
    /// <param name="exception">The escaped exception.</param>
    void PresentUnhandledException(Exception exception);

    /// <summary>
    /// Presents a startup failure that blocks the shell from opening.
    /// </summary>
    /// <param name="code">The stable event code; it selects the registered presentation and detail text.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="exception">The exception behind the failure.</param>
    void PresentFailure(string code, string operation, Exception exception);
}
