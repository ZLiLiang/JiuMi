namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Imports complete video files that LiteApp already preloaded into Weixin's local cache.
/// </summary>
public interface IWeChatPreloadCacheImporter
{
    /// <summary>
    /// Reads the LiteApp preload records and writes every complete cache file into the output
    /// directory after removing the encrypted head. Incomplete caches are skipped.
    /// </summary>
    /// <param name="outputDirectory">The existing output directory.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>The import outcome.</returns>
    Task<WeChatPreloadImportResult> ImportAsync(
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
