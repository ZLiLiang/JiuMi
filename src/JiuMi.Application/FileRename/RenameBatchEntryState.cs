namespace JiuMi.Application.FileRename;

/// <summary>
/// What happened to one item of the last batch.
/// </summary>
public enum RenameBatchEntryState
{
    /// <summary>The item was renamed.</summary>
    Renamed,

    /// <summary>The item could not be renamed.</summary>
    Failed,

    /// <summary>The item was put back to its original name.</summary>
    Undone,

    /// <summary>The item could not be put back; it keeps its renamed state.</summary>
    UndoFailed,
}
