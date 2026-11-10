namespace JiuMi.Application.FileRename;

/// <summary>
/// The three tasks offered by the basic level: each one is converted into a regular rule.
/// </summary>
public enum RenameBasicTask
{
    /// <summary>Replace literal text.</summary>
    Replace,

    /// <summary>Add text before or after the name.</summary>
    Affix,

    /// <summary>Rename by sequence number.</summary>
    Sequence,
}
