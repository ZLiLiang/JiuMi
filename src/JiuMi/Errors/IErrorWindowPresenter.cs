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
}
