namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One complete video written by the LiteApp preload cache import.
/// </summary>
/// <param name="FeedId">The LiteApp feed identifier.</param>
/// <param name="SavedPath">The written MP4 path.</param>
/// <param name="FileSize">The written file size.</param>
public sealed record WeChatPreloadImportedVideo(string FeedId, string SavedPath, long FileSize);
