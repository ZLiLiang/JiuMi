namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The decrypt seed LiteApp persisted for a preloaded video.
/// </summary>
/// <param name="FeedId">The LiteApp feed identifier.</param>
/// <param name="Seed">The numeric <c>decode_key</c>.</param>
/// <param name="VideoContentLength">The full video length recorded by LiteApp.</param>
/// <param name="CreateTime">The LiteApp preload record time in Unix milliseconds.</param>
public sealed record WeChatPreloadSeed(
    string FeedId,
    ulong Seed,
    long VideoContentLength,
    long CreateTime);
