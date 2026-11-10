namespace JiuMi.Application.FileRename;

/// <summary>
/// Why a scanned item is listed as skipped instead of being renamed.
/// </summary>
public enum RenameSkipReason
{
    /// <summary>The item carries the hidden attribute.</summary>
    Hidden,

    /// <summary>The item carries the system attribute.</summary>
    System,

    /// <summary>The item is a symbolic link.</summary>
    SymbolicLink,

    /// <summary>The item is a directory junction.</summary>
    Junction,
}
