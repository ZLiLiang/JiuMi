namespace JiuMi.Application.FileRename;

/// <summary>
/// What one source contributed to the scan, before the rule is applied.
/// </summary>
/// <param name="Source">The source that was scanned or added.</param>
/// <param name="TotalCount">
/// The items that could enter the batch, before the extension filter and before de-duplication.
/// </param>
/// <param name="ContributionCount">
/// The items kept for this source after filtering and de-duplication; skipped items do not count.
/// </param>
/// <param name="FilteredPaths">The absolute paths the extension filter left out, in discovery order.</param>
public sealed record RenameSourceScanSummary(
    RenameSource Source,
    int TotalCount,
    int ContributionCount,
    IReadOnlyList<string> FilteredPaths)
{
    /// <summary>Gets the number of items the extension filter left out.</summary>
    public int FilteredCount => FilteredPaths.Count;
}
