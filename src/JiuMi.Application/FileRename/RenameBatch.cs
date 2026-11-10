namespace JiuMi.Application.FileRename;

/// <summary>
/// The last rename batch. Only one batch is kept per session; undo and log export read it.
/// </summary>
/// <param name="ExecutedAt">When the batch ran.</param>
/// <param name="BackupEnabled">Whether a safety copy was requested.</param>
/// <param name="BackupDirectory">The safety copy directory, or <see langword="null"/>.</param>
/// <param name="Entries">One entry per attempted item.</param>
public sealed record RenameBatch(
    DateTimeOffset ExecutedAt,
    bool BackupEnabled,
    string? BackupDirectory,
    IReadOnlyList<RenameBatchEntry> Entries)
{
    /// <summary>Gets the number of renamed items.</summary>
    public int RenamedCount => Entries.Count(entry => entry.State == RenameBatchEntryState.Renamed);

    /// <summary>Gets the number of items that failed to rename.</summary>
    public int FailedCount => Entries.Count(entry => entry.State == RenameBatchEntryState.Failed);

    /// <summary>Gets the number of items that were put back.</summary>
    public int UndoneCount => Entries.Count(entry => entry.State == RenameBatchEntryState.Undone);

    /// <summary>Gets the number of items that could not be put back.</summary>
    public int UndoFailedCount => Entries.Count(entry => entry.State == RenameBatchEntryState.UndoFailed);

    /// <summary>Gets a value indicating whether undo already ran for this batch.</summary>
    public bool IsUndone =>
        Entries.Any(entry => entry.State is RenameBatchEntryState.Undone or RenameBatchEntryState.UndoFailed);
}
