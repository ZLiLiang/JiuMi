namespace JiuMi.ViewModels;

/// <summary>
/// The five states a rename preview row can show.
/// </summary>
public enum FileRenameRowStatus
{
    /// <summary>The row will be renamed.</summary>
    Pending,

    /// <summary>Precheck blocks the row.</summary>
    Conflict,

    /// <summary>The row was renamed.</summary>
    Done,

    /// <summary>The row could not be renamed.</summary>
    Failed,

    /// <summary>The row could not be put back by undo.</summary>
    UndoFailed,
}
