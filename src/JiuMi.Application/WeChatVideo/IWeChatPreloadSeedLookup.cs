namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Finds the LiteApp preload seed that belongs to a captured media response.
/// </summary>
public interface IWeChatPreloadSeedLookup
{
    /// <summary>
    /// Finds the closest preload record with the same full video length.
    /// </summary>
    /// <param name="videoContentLength">The total media size from the response.</param>
    /// <param name="capturedAt">When the media response was observed.</param>
    /// <returns>The matching seed, or <see langword="null"/> when no record matches.</returns>
    WeChatPreloadSeed? Find(long videoContentLength, DateTimeOffset capturedAt);
}
