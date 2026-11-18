namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The download state of one video in the session list.
/// </summary>
public enum WeChatVideoDownloadState
{
    /// <summary>The video has been captured and not downloaded yet.</summary>
    Idle,

    /// <summary>The video is waiting for the serial download queue.</summary>
    Queued,

    /// <summary>The video is being downloaded.</summary>
    Downloading,

    /// <summary>The video was written to the output directory.</summary>
    Completed,

    /// <summary>The download failed and may be retried by the user.</summary>
    Failed,

    /// <summary>The media link expired; the page has to be opened again.</summary>
    Expired,

    /// <summary>The user canceled the download.</summary>
    Canceled,
}
