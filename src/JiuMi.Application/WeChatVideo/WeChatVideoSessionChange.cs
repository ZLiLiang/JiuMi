namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// What happened to a video in the session list.
/// </summary>
public enum WeChatVideoSessionChange
{
    /// <summary>The video entered the list.</summary>
    Added,

    /// <summary>The video's capture or download state changed.</summary>
    Updated,

    /// <summary>The video left the list.</summary>
    Removed,
}
