namespace JiuMi.Application.FileRename;

/// <summary>
/// Everything found under the selected directory, including the skipped items.
/// </summary>
/// <param name="Candidates">The scoped items; the selected directory itself is never included.</param>
public sealed record RenameScanResult(IReadOnlyList<RenameCandidate> Candidates);
