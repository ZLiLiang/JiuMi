namespace JiuMi.Application.FileRename;

/// <summary>
/// One file or folder found under the selected directory, before the rule is applied.
/// </summary>
/// <param name="FullPath">The absolute path of the item.</param>
/// <param name="RelativePath">The path below the selected directory, using forward slashes.</param>
/// <param name="IsDirectory">Whether the item is a folder.</param>
/// <param name="SkipReason">Why the item is skipped, or <see langword="null"/> when it may be renamed.</param>
public sealed record RenameCandidate(
    string FullPath,
    string RelativePath,
    bool IsDirectory,
    RenameSkipReason? SkipReason = null);
