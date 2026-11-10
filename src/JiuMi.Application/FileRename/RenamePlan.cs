namespace JiuMi.Application.FileRename;

/// <summary>
/// The preview of one batch: the rows that will change, the skipped items and the count the rule
/// did not match. The plan is immutable and contains no file-system reads of its own.
/// </summary>
/// <param name="Entries">The rows in preview order, numbered from one.</param>
/// <param name="Skipped">The hidden, system and linked items left out.</param>
/// <param name="UnmatchedCount">The number of scoped items the rule did not match or left unchanged.</param>
public sealed record RenamePlan(
    IReadOnlyList<RenamePlanEntry> Entries,
    IReadOnlyList<RenameSkippedItem> Skipped,
    int UnmatchedCount)
{
    /// <summary>Gets the number of rows shown.</summary>
    public int AffectedCount => Entries.Count;

    /// <summary>Gets the number of rows blocked by a precheck error.</summary>
    public int ConflictCount => Entries.Count(entry => entry.IsConflict);

    /// <summary>Gets the number of rows that can be renamed.</summary>
    public int RenamableCount => Entries.Count(entry => entry.CanRename);

    /// <summary>Gets a value indicating whether any row is blocked.</summary>
    public bool HasConflicts => ConflictCount > 0;

    /// <summary>Gets a value indicating whether the batch may run.</summary>
    public bool CanExecute => RenamableCount > 0 && !HasConflicts;

    /// <summary>Gets the number of runnable rows whose extension changes.</summary>
    public int ExtensionChangeCount => Entries.Count(entry => entry.CanRename && entry.ChangesExtension);

    /// <summary>Gets each distinct precheck error, in the order the rows report it.</summary>
    public IReadOnlyList<RenamePrecheckError> DistinctErrors
    {
        get
        {
            var errors = new List<RenamePrecheckError>();
            foreach (RenamePlanEntry entry in Entries)
            {
                if (entry.Error is { } error && !errors.Contains(error))
                {
                    errors.Add(error);
                }
            }

            return errors;
        }
    }
}
