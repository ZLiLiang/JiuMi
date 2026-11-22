namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One <c>videoPlayStart</c> telemetry event extracted from WeChat process memory.
/// </summary>
/// <param name="FeedId">The numeric WeChat Finder feed identifier.</param>
/// <param name="PlayId">
/// The per-playback UUID when present in the event payload, allowing replays of previously
/// watched videos to be distinguished from pre-existing baseline events.
/// </param>
public sealed record WeChatPlaybackStartEvent(string FeedId, string? PlayId)
{
    /// <summary>
    /// Gets the deduplication key for baseline and poll filtering.
    /// </summary>
    public string EventKey => !string.IsNullOrWhiteSpace(PlayId)
        ? "play:" + PlayId
        : "feed:" + FeedId;
}
