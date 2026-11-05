namespace JiuMi.Application.FileRename;

/// <summary>
/// A scan over every source the page currently holds.
/// </summary>
/// <param name="Sources">The folder and file sources, in add order.</param>
/// <param name="Target">Whether files or folders enter the scan.</param>
/// <param name="IncludeSubdirectories">Whether the whole subtree of every folder source is visited.</param>
/// <param name="Extensions">The extension filter for files; empty means no filter.</param>
public sealed record RenameScanRequest(
    IReadOnlyList<RenameSource> Sources,
    RenameTarget Target,
    bool IncludeSubdirectories,
    string Extensions);
