namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One quality variant reported in a WeChat Finder media <c>spec</c> array.
/// </summary>
public sealed record WeChatVideoSpec
{
    /// <summary>
    /// Gets the format identifier (such as <c>xWT111</c>).
    /// </summary>
    public string? FileFormat
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the codec identifier (such as <c>h264</c> or <c>h265</c>), when reported.
    /// </summary>
    public string? CodingFormat
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the video width in pixels, when reported.
    /// </summary>
    public int? Width
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the video height in pixels, when reported.
    /// </summary>
    public int? Height
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the reported bitrate, when available.
    /// </summary>
    public int? BitRate
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the reported video bitrate in kbps, when available.
    /// </summary>
    public int? VideoBitRate
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the reported audio bitrate in kbps, when available.
    /// </summary>
    public int? AudioBitRate
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the reported video frame rate, when available.
    /// </summary>
    public double? Vfps
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the reported file size in bytes, when available.
    /// </summary>
    public long? FileSize
    {
        get;
        init;
    }
}
