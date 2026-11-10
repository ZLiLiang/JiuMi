namespace JiuMi.Application.FileRename;

/// <summary>
/// A whole batch handed to the executor.
/// </summary>
/// <param name="BackupRoot">
/// The directory that receives the <c>JiuMi-backup-&lt;timestamp&gt;</c> folder. The caller derives
/// it from the first source: the parent of its directory.
/// </param>
/// <param name="Items">The pairs to rename, in preview order.</param>
/// <param name="CreateBackup">Whether to copy each item before renaming it.</param>
public sealed record RenameExecutionRequest(
    string BackupRoot,
    IReadOnlyList<RenameExecutionItem> Items,
    bool CreateBackup);
