namespace JiuMi.Application.FileRename;

/// <summary>
/// Everything the sources contributed, including the skipped and filtered items.
/// </summary>
/// <param name="Candidates">The scoped items; a folder source itself is never included.</param>
/// <param name="Sources">One summary per source, in add order.</param>
public sealed record RenameScanResult(
    IReadOnlyList<RenameCandidate> Candidates,
    IReadOnlyList<RenameSourceScanSummary> Sources);
