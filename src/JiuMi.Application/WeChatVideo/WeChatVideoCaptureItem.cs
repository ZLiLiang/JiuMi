namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One video reported by the injected page script.
/// </summary>
public sealed record WeChatVideoCaptureItem
{
    /// <summary>
    /// Gets the media URL including its one-time token.
    /// </summary>
    public required string Url
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the page the video was opened from.
    /// </summary>
    public required string PageUrl
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the key that seeds the XOR key stream, when the page supplies one.
    /// </summary>
    public string? DecodeKey
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the cover image URL.
    /// </summary>
    public string? ThumbUrl
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the video description shown by WeChat.
    /// </summary>
    public string? Description
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the uploader nickname.
    /// </summary>
    public string? Uploader
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the size WeChat reports, which is not the size the server serves.
    /// </summary>
    public long? ReportedFileSize
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the playback length in seconds.
    /// </summary>
    public int? VideoLengthSeconds
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the feed object identifier, used to recognise the same video opened repeatedly.
    /// Discovery captures use the feed item identifier here.
    /// </summary>
    public string? Oid
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the feed item identifier, used together with <see cref="Oid"/> for de-duplication.
    /// Discovery captures use a fixed marker here because the home page has no feed identifiers.
    /// </summary>
    public string? Nid
    {
        get;
        init;
    }
}
