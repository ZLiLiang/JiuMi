using System.Diagnostics.CodeAnalysis;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Views;

namespace JiuMi.Errors;

/// <summary>
/// Presents failures in the single error window, or leaves the process when that window cannot be
/// shown or is already showing.
/// </summary>
public sealed class ErrorWindowPresenter : IErrorWindowPresenter
{
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly ILocalizationService _localization;
    private readonly ErrorWindowCoordinator _coordinator;
    private readonly Func<ErrorWindow> _createWindow;
    private readonly Func<bool> _isMainWindowAvailable;
    private readonly Action<int> _requestExit;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorWindowPresenter"/> class.
    /// </summary>
    /// <param name="errorReporter">Stores the record that becomes the copied details.</param>
    /// <param name="recordFactory">Creates the record for an escaped exception.</param>
    /// <param name="localization">Supplies the first line of the copied details.</param>
    /// <param name="coordinator">Guarantees at most one error window per process.</param>
    /// <param name="createWindow">Creates the window once a failure must be shown.</param>
    /// <param name="isMainWindowAvailable">
    /// Reports whether the main window is usable, which decides between the blocking and the
    /// continue-running variant.
    /// </param>
    /// <param name="requestExit">Leaves the process with the given exit code.</param>
    public ErrorWindowPresenter(
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        ILocalizationService localization,
        ErrorWindowCoordinator coordinator,
        Func<ErrorWindow> createWindow,
        Func<bool> isMainWindowAvailable,
        Action<int> requestExit)
    {
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(createWindow);
        ArgumentNullException.ThrowIfNull(isMainWindowAvailable);
        ArgumentNullException.ThrowIfNull(requestExit);

        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _localization = localization;
        _coordinator = coordinator;
        _createWindow = createWindow;
        _isMainWindowAvailable = isMainWindowAvailable;
        _requestExit = requestExit;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The error window is the last reported surface; failing to show it must exit instead of crashing.")]
    public void PresentUnhandledException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            ErrorRecord reported = _recordFactory.Create(
                ErrorCodes.UnexpectedUiThreadException,
                "Dispatcher.UnhandledException",
                exception);
            _errorReporter.Report(reported);

            // The buffer owns the merged count, so the copied details read the stored record. The first
            // line is the localized Error.<code> text so the copy matches the info bar byte for byte.
            ErrorRecord stored = _errorReporter.Details.Find(reported.Code) ?? reported;
            ErrorWindowRequest? request = _coordinator.Open(
                stored,
                _isMainWindowAvailable(),
                _localization["Error." + stored.Code]);

            if (request is null)
            {
                // One window per process: a second failure cannot stack another window.
                _requestExit(ExitCodes.Failure);
                return;
            }

            ErrorWindow window = _createWindow();
            window.Closed += OnWindowClosed;
            window.ShowFailure(request, _requestExit, ReportCopyFailure);
        }
        catch (Exception failure)
        {
            _coordinator.Close();
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.UnexpectedErrorWindowFailed,
                "ErrorWindow.Show",
                failure));
            _requestExit(ExitCodes.Failure);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is ErrorWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _coordinator.Close();
    }

    private void ReportCopyFailure(Exception error) =>
        _errorReporter.Report(_recordFactory.Create(ErrorCodes.UnexpectedCopyFailed, "Clipboard.Copy", error));
}
