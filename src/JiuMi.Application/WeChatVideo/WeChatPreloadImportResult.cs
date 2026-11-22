namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The outcome of importing complete LiteApp preload cache files.
/// </summary>
/// <param name="Imported">How many complete videos were written.</param>
/// <param name="Partial">How many cache files were incomplete and skipped.</param>
/// <param name="Failed">How many complete cache files could not be processed.</param>
/// <param name="Skipped">How many videos were already present in the output directory.</param>
/// <param name="ImportedVideos">The videos written by the import.</param>
public sealed record WeChatPreloadImportResult(
    int Imported,
    int Partial,
    int Failed,
    int Skipped,
    IReadOnlyList<WeChatPreloadImportedVideo> ImportedVideos);
