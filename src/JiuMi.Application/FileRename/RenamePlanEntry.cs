namespace JiuMi.Application.FileRename;

/// <summary>
/// One row of a rename preview: the source, its number and the planned target.
/// </summary>
/// <param name="Candidate">The source item.</param>
/// <param name="Index">The one-based number shown to the user and used by index tokens.</param>
/// <param name="TargetName">The new name, without a directory.</param>
/// <param name="TargetFullPath">The new absolute path.</param>
/// <param name="Error">Why the row cannot be renamed, or <see langword="null"/> when it can.</param>
/// <param name="ChangesExtension">Whether the extension changes, compared case-sensitively.</param>
public sealed record RenamePlanEntry(
    RenameCandidate Candidate,
    int Index,
    string TargetName,
    string TargetFullPath,
    RenamePrecheckError? Error,
    bool ChangesExtension)
{
    /// <summary>Gets the original name, without a directory.</summary>
    public string OriginalName => Path.GetFileName(Candidate.FullPath);

    /// <summary>Gets a value indicating whether the row has a precheck error.</summary>
    public bool IsConflict => Error is not null;

    /// <summary>Gets a value indicating whether the row can be renamed.</summary>
    public bool CanRename => Error is null;
}
