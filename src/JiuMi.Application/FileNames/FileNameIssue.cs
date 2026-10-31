namespace JiuMi.Application.FileNames;

/// <summary>
/// The reason a Windows file name cannot be used as a rename target.
/// </summary>
public enum FileNameIssue
{
    /// <summary>The name is usable.</summary>
    None,

    /// <summary>The name is empty.</summary>
    Empty,

    /// <summary>The name contains a character Windows reserves for the file system.</summary>
    IllegalCharacter,

    /// <summary>The name ends with a space or a dot.</summary>
    TrailingSpaceOrDot,

    /// <summary>The name is a device name such as CON or COM1.</summary>
    ReservedName,

    /// <summary>The name or the full path exceeds the Windows limit.</summary>
    TooLong,
}
