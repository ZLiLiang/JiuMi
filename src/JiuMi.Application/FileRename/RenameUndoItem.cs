namespace JiuMi.Application.FileRename;

/// <summary>
/// One item that can be put back by undo.
/// </summary>
/// <param name="OriginalPath">The path to restore.</param>
/// <param name="RenamedPath">The current path.</param>
/// <param name="IsDirectory">Whether the item is a folder.</param>
/// <param name="Stamp">The file fingerprint captured after renaming, or <see langword="null"/> for folders.</param>
public sealed record RenameUndoItem(
    string OriginalPath,
    string RenamedPath,
    bool IsDirectory,
    RenameFileStamp? Stamp);
