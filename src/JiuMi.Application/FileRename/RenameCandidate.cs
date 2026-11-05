namespace JiuMi.Application.FileRename;

/// <summary>
/// One file or folder found in a source, before the rule is applied.
/// </summary>
/// <param name="FullPath">The absolute path of the item.</param>
/// <param name="RelativePath">
/// The path below the item's own source, using forward slashes; a directly added file is known by
/// its file name.
/// </param>
/// <param name="IsDirectory">Whether the item is a folder.</param>
/// <param name="SourceOrder">The add order of the source that contributed the item.</param>
/// <param name="SkipReason">Why the item is skipped, or <see langword="null"/> when it may be renamed.</param>
public sealed record RenameCandidate(
    string FullPath,
    string RelativePath,
    bool IsDirectory,
    int SourceOrder,
    RenameSkipReason? SkipReason = null);
