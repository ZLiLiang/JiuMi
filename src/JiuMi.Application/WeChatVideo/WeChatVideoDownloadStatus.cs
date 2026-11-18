namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The outcome of one download attempt.
/// </summary>
public enum WeChatVideoDownloadStatus
{
    /// <summary>The video was written to the output directory.</summary>
    Completed,

    /// <summary>The media link expired; the page has to be opened again.</summary>
    Expired,

    /// <summary>The download failed and may be retried by the user.</summary>
    Failed,

    /// <summary>The user canceled the download.</summary>
    Canceled,
}
