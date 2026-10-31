namespace JiuMi.Application.FileRename;

/// <summary>
/// Why a preview row cannot be renamed. Any error disables the run; the target is never repaired.
/// </summary>
public enum RenamePrecheckError
{
    /// <summary>The target name is empty.</summary>
    EmptyName,

    /// <summary>The target name contains an illegal or control character.</summary>
    IllegalCharacter,

    /// <summary>The target name ends with a space or a dot.</summary>
    TrailingSpaceOrDot,

    /// <summary>The target name is a Windows device name.</summary>
    ReservedName,

    /// <summary>The target name or path is too long.</summary>
    TooLong,

    /// <summary>More than one row in the batch targets the same name.</summary>
    Duplicate,

    /// <summary>Another file or folder already uses the target name.</summary>
    Occupied,
}
