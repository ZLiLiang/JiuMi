namespace JiuMi.Application.FileRename;

/// <summary>
/// Why one item could not be renamed, or could not be put back during undo.
/// </summary>
public enum RenameFailure
{
    /// <summary>The source item no longer exists.</summary>
    SourceMissing,

    /// <summary>Another item already uses the target name.</summary>
    TargetExists,

    /// <summary>Windows denied access to the item.</summary>
    AccessDenied,

    /// <summary>The safety copy could not be written.</summary>
    BackupFailed,

    /// <summary>The item was changed by another program before undo.</summary>
    Modified,

    /// <summary>The item failed for another reason.</summary>
    Unknown,
}
