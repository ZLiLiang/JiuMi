namespace JiuMi.Application.FileRename;

/// <summary>
/// A scan of one selected directory.
/// </summary>
/// <param name="DirectoryPath">The selected directory; it is only referenced, never renamed.</param>
/// <param name="Target">Whether files or folders enter the scan.</param>
/// <param name="IncludeSubdirectories">Whether the whole subtree is visited.</param>
/// <param name="Extensions">The extension filter for files; empty means no filter.</param>
public sealed record RenameScanRequest(
    string DirectoryPath,
    RenameTarget Target,
    bool IncludeSubdirectories,
    string Extensions);
