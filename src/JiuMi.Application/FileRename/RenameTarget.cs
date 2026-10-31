namespace JiuMi.Application.FileRename;

/// <summary>
/// Whether the page renames files or folders; never both in one batch.
/// </summary>
public enum RenameTarget
{
    /// <summary>Rename files.</summary>
    Files,

    /// <summary>Rename folders.</summary>
    Folders,
}
