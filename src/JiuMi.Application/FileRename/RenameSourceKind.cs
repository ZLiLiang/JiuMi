namespace JiuMi.Application.FileRename;

/// <summary>
/// Whether a source is a folder to scan or the files selected together in one add action.
/// </summary>
public enum RenameSourceKind
{
    /// <summary>A folder that only supplies the files inside it; the folder itself is never renamed.</summary>
    Folder,

    /// <summary>Files added directly; they enter the batch as they are.</summary>
    Files,
}
