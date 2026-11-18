namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One video download request.
/// </summary>
public sealed record WeChatVideoDownloadRequest
{
    /// <summary>
    /// Gets the media URL, including its one-time token.
    /// </summary>
    public required Uri MediaUrl
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the user's output directory. It must already exist.
    /// </summary>
    public required string OutputDirectory
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the cleaned file stem; the downloader appends <c>.mp4</c>.
    /// </summary>
    public required string FileStem
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the <c>decode_key</c> used to decrypt the head of an encrypted video.
    /// </summary>
    public string? DecodeKey
    {
        get;
        init;
    }
}
