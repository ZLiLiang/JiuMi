using System.Diagnostics.CodeAnalysis;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;

namespace JiuMi.Diagnostics;

/// <summary>
/// Owns the diagnostics export operation: one at a time, remembering the destination for the info
/// bar's retry action and reporting failures the same way the rest of the shell does.
/// </summary>
public sealed class DiagnosticsExportController : IDisposable
{
    private readonly IDiagnosticsExporter _exporter;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _lastDestinationPath;
    private bool _isExporting;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsExportController"/> class.
    /// </summary>
    /// <param name="exporter">Writes the bundle.</param>
    /// <param name="errorReporter">Receives export failures.</param>
    /// <param name="recordFactory">Creates the failure record.</param>
    public DiagnosticsExportController(
        IDiagnosticsExporter exporter,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);

        _exporter = exporter;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
    }

    /// <summary>
    /// Raised on the interface thread when an export starts or ends.
    /// </summary>
    public event EventHandler? ExportingChanged;

    /// <summary>
    /// Gets a value indicating whether an export is running.
    /// </summary>
    public bool IsExporting => _isExporting;

    /// <summary>
    /// Exports the bundle to the destination the user chose.
    /// </summary>
    /// <param name="destinationPath">The zip file to create.</param>
    /// <returns>The outcome, with failures already reported.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The export is a user action; an unexpected failure is reported through the info bar instead of escaping the click handler.")]
    public async Task<DiagnosticsExportResult> ExportAsync(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        await _gate.WaitAsync();
        SetExporting(true);
        _lastDestinationPath = destinationPath;

        try
        {
            DiagnosticsExportResult result = await _exporter.ExportAsync(destinationPath);
            if (!result.Succeeded)
            {
                ReportFailure(result.Error ?? new InvalidOperationException("The export failed without an error."));
            }

            return result;
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
            return DiagnosticsExportResult.Failed(destinationPath, exception);
        }
        finally
        {
            SetExporting(false);
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs the last export again, for the info bar's retry action.
    /// </summary>
    /// <returns>The outcome, with failures already reported.</returns>
    public Task<DiagnosticsExportResult> RetryLastAsync()
    {
        if (_lastDestinationPath is { } destinationPath)
        {
            return ExportAsync(destinationPath);
        }

        // A failure before the save dialog chose a path has nothing to rerun; report it again so the
        // notice count reflects the retry instead of silently doing nothing.
        ReportFailure(exception: null);
        return Task.FromResult(DiagnosticsExportResult.NotAttempted());
    }

    /// <summary>
    /// Reports an export failure, including one that happened before the exporter ran.
    /// </summary>
    /// <param name="exception">The failure, or <see langword="null"/> when nothing was attempted.</param>
    public void ReportFailure(Exception? exception)
    {
        _errorReporter.Report(_recordFactory.Create(
            ErrorCodes.DiagnosticsExportFailed,
            "Diagnostics.Export",
            exception));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _gate.Dispose();
    }

    private void SetExporting(bool isExporting)
    {
        if (_isExporting == isExporting)
        {
            return;
        }

        _isExporting = isExporting;
        ExportingChanged?.Invoke(this, EventArgs.Empty);
    }
}
