namespace JiuMi.Application.FileRename;

/// <summary>
/// Reads the selected directory. Hidden, system and linked items are reported with a skip reason
/// instead of being followed.
/// </summary>
public interface IRenameScanner
{
    /// <summary>
    /// Scans the selected directory for files or folders.
    /// </summary>
    /// <param name="request">The directory, the object type and the scope.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The scoped items, including those that will be skipped.</returns>
    Task<RenameScanResult> ScanAsync(
        RenameScanRequest request,
        CancellationToken cancellationToken = default);
}
