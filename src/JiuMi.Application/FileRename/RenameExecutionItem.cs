namespace JiuMi.Application.FileRename;

/// <summary>
/// One source and target pair handed to the executor.
/// </summary>
/// <param name="SourcePath">The current absolute path.</param>
/// <param name="TargetPath">The absolute path to rename to.</param>
/// <param name="IsDirectory">Whether the item is a folder.</param>
/// <param name="BackupGroup">
/// The backup subfolder that groups the item with its source, such as <c>1-photos</c>.
/// </param>
public sealed record RenameExecutionItem(
    string SourcePath,
    string TargetPath,
    bool IsDirectory,
    string BackupGroup);
