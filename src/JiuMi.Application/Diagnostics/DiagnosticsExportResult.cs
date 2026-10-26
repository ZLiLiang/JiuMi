namespace JiuMi.Application.Diagnostics;

/// <summary>
/// The outcome of writing a diagnostics bundle.
/// </summary>
/// <param name="Succeeded">Whether the bundle was written.</param>
/// <param name="DestinationPath">The zip path that was requested.</param>
/// <param name="Error">The failure, or <see langword="null"/> when the write succeeded.</param>
public sealed record DiagnosticsExportResult(
    bool Succeeded,
    string DestinationPath,
    Exception? Error)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="destinationPath">The zip that was written.</param>
    /// <returns>The successful outcome.</returns>
    public static DiagnosticsExportResult Success(string destinationPath) =>
        new(true, destinationPath, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="destinationPath">The zip that could not be written.</param>
    /// <param name="error">The failure.</param>
    /// <returns>The failed outcome.</returns>
    public static DiagnosticsExportResult Failed(string destinationPath, Exception error) =>
        new(false, destinationPath, error);

    /// <summary>
    /// Creates the result of a retry that had no destination to write to yet.
    /// </summary>
    /// <returns>The failed outcome without an exception, because nothing was attempted.</returns>
    public static DiagnosticsExportResult NotAttempted() => new(false, string.Empty, null);
}
