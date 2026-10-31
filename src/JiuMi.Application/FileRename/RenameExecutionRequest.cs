namespace JiuMi.Application.FileRename;

/// <summary>
/// A whole batch handed to the executor.
/// </summary>
/// <param name="SourceRoot">The selected directory, used to place the safety copy beside it.</param>
/// <param name="Items">The pairs to rename, in preview order.</param>
/// <param name="CreateBackup">Whether to copy each item before renaming it.</param>
public sealed record RenameExecutionRequest(
    string SourceRoot,
    IReadOnlyList<RenameExecutionItem> Items,
    bool CreateBackup);
