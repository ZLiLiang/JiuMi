namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One captured video together with its session download state.
/// </summary>
public sealed record WeChatVideoItem
{
    /// <summary>
    /// Gets the de-duplication key. Its format is opaque; compare it only with another key.
    /// </summary>
    public required string Key
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the latest capture the page reported for this video.
    /// </summary>
    public required WeChatVideoCaptureItem Capture
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the local time the video first entered the list.
    /// </summary>
    public required DateTimeOffset CapturedAt
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the download state.
    /// </summary>
    public WeChatVideoDownloadState State
    {
        get;
        init;
    }

    /// <summary>
    /// Gets why the last download attempt failed, when it did.
    /// </summary>
    public WeChatVideoDownloadFailure Failure
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the number of bytes written to the partial file.
    /// </summary>
    public long DownloadedBytes
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the total bytes the server reported, when known.
    /// </summary>
    public long? TotalBytes
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the finished file path, when the download completed.
    /// </summary>
    public string? SavedPath
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the user-selected <c>file_format</c> quality variant, or <see langword="null"/> to
    /// download the default stream returned by WeChat.
    /// </summary>
    public string? SelectedFileFormat
    {
        get;
        init;
    }

    /// <summary>
    /// Gets a value indicating whether the quality variant may be changed in the current download state.
    /// </summary>
    public bool CanSelectQuality =>
        State is WeChatVideoDownloadState.Idle
            or WeChatVideoDownloadState.Failed
            or WeChatVideoDownloadState.Canceled;
}
