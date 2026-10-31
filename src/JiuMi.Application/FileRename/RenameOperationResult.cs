namespace JiuMi.Application.FileRename;

/// <summary>
/// The outcome of a whole batch.
/// </summary>
/// <param name="Items">One outcome per item that was attempted.</param>
/// <param name="BackupDirectory">The safety copy directory, or <see langword="null"/> when backups are off.</param>
public sealed record RenameOperationResult(
    IReadOnlyList<RenameItemOutcome> Items,
    string? BackupDirectory = null);
