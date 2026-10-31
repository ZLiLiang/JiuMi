namespace JiuMi.Application.FileRename;

/// <summary>
/// Renames and restores files and folders. Implementations perform the file-system work; the
/// preview never calls them.
/// </summary>
public interface IRenameExecutor
{
    /// <summary>
    /// Renames every item in the request, continuing after a single item fails.
    /// </summary>
    /// <param name="request">The batch to rename.</param>
    /// <param name="progress">Receives each item outcome as it completes, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops after the item in progress.</param>
    /// <returns>One outcome per attempted item.</returns>
    Task<RenameOperationResult> ExecuteAsync(
        RenameExecutionRequest request,
        IProgress<RenameItemOutcome>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts items back in reverse order after verifying each one was not changed outside JiuMi.
    /// </summary>
    /// <param name="items">The items to restore.</param>
    /// <param name="cancellationToken">Stops after the item in progress.</param>
    /// <returns>One outcome per attempted item.</returns>
    Task<RenameOperationResult> UndoAsync(
        IReadOnlyList<RenameUndoItem> items,
        CancellationToken cancellationToken = default);
}
