namespace JiuMi.Application.FileRename;

/// <summary>
/// The result of one rename or undo step.
/// </summary>
/// <param name="SourcePath">The original path.</param>
/// <param name="TargetPath">The path after a successful rename; unchanged on failure.</param>
/// <param name="IsDirectory">Whether the item is a folder.</param>
/// <param name="Failure">Why the step failed, or <see langword="null"/> when it succeeded.</param>
/// <param name="Stamp">The fingerprint captured after a successful file rename.</param>
public sealed record RenameItemOutcome(
    string SourcePath,
    string TargetPath,
    bool IsDirectory,
    RenameFailure? Failure,
    RenameFileStamp? Stamp = null)
{
    /// <summary>Gets a value indicating whether the step succeeded.</summary>
    public bool Succeeded => Failure is null;
}
