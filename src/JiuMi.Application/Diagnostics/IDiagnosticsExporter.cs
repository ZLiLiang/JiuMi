namespace JiuMi.Application.Diagnostics;

/// <summary>
/// Packs the current log files, crash files and environment summary into a zip chosen by the user.
/// </summary>
public interface IDiagnosticsExporter
{
    /// <summary>
    /// Gets the directory the current session logs to, or <see langword="null"/> when file logging is
    /// unavailable.
    /// </summary>
    string? LogDirectory
    {
        get;
    }

    /// <summary>
    /// Creates the default file name offered by the save dialog.
    /// </summary>
    /// <returns>A zip name carrying the current local time.</returns>
    string CreateDefaultFileName();

    /// <summary>
    /// Writes the diagnostics bundle to <paramref name="destinationPath"/>.
    /// </summary>
    /// <param name="destinationPath">The zip file to create, replacing an existing file.</param>
    /// <param name="cancellationToken">Cancels the export and removes the partial file.</param>
    /// <returns>The outcome; failures are returned rather than thrown to the interface.</returns>
    Task<DiagnosticsExportResult> ExportAsync(
        string destinationPath,
        CancellationToken cancellationToken = default);
}
