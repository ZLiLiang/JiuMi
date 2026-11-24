namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// One selectable quality option presented to the user for a captured WeChat video.
/// A <see langword="null"/> <see cref="FileFormat"/> represents the default stream returned by WeChat.
/// </summary>
public sealed record WeChatVideoSpecOption
{
    /// <summary>
    /// Gets the CDN <c>file_format</c> identifier (such as <c>xWT101</c>), or <see langword="null"/>
    /// for the default stream without <c>X-snsvideoflag</c>.
    /// </summary>
    public string? FileFormat
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the concise display label shown in the selector (for example <c>默认</c> or <c>1080p · H.265</c>).
    /// </summary>
    public required string Label
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the video width in pixels, when known.
    /// </summary>
    public int? Width
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the video height in pixels, when known.
    /// </summary>
    public int? Height
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the normalized codec display name (such as <c>H.265</c> or <c>H.264</c>), when known.
    /// </summary>
    public string? CodingFormat
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the effective bitrate used for sorting, when known.
    /// </summary>
    public int? EffectiveBitRate
    {
        get;
        init;
    }

    /// <summary>
    /// Gets a value indicating whether this option downloads the default WeChat stream.
    /// </summary>
    public bool IsDefault => FileFormat is null;
}
