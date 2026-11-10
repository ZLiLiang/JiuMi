namespace JiuMi.Application.FileRename;

/// <summary>
/// One item left out of the batch because of its attributes or link type.
/// </summary>
/// <param name="Candidate">The skipped item.</param>
/// <param name="Reason">Why it is skipped.</param>
public sealed record RenameSkippedItem(RenameCandidate Candidate, RenameSkipReason Reason);
