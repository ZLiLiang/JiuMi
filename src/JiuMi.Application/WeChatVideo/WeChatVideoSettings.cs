namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The WeChat video download parameters kept between sessions.
/// </summary>
public sealed record WeChatVideoSettings
{
    /// <summary>
    /// Gets the directory that receives downloaded videos, or <see langword="null"/> when the user has not chosen one.
    /// </summary>
    public string? DownloadDirectory
    {
        get;
        init;
    }

    /// <summary>
    /// Creates the settings used before the user changes anything.
    /// </summary>
    /// <returns>The defaults: no download directory.</returns>
    public static WeChatVideoSettings CreateDefault() => new();
}
