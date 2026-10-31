namespace JiuMi.Application.FileRename;

/// <summary>
/// One item of the last batch, as shown by the footer and written to the log.
/// </summary>
/// <param name="OriginalPath">The path before the batch.</param>
/// <param name="RenamedPath">The path after the batch.</param>
/// <param name="IsDirectory">Whether the item is a folder.</param>
/// <param name="Stamp">The file fingerprint captured after renaming, or <see langword="null"/>.</param>
/// <param name="State">What happened to the item.</param>
/// <param name="Failure">Why the item failed, or <see langword="null"/>.</param>
public sealed record RenameBatchEntry(
    string OriginalPath,
    string RenamedPath,
    bool IsDirectory,
    RenameFileStamp? Stamp,
    RenameBatchEntryState State,
    RenameFailure? Failure = null)
{
    /// <summary>Gets the original name, without a directory.</summary>
    public string OriginalName => Path.GetFileName(OriginalPath);

    /// <summary>Gets the renamed name, without a directory.</summary>
    public string RenamedName => Path.GetFileName(RenamedPath);
}
