namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Why a download did not complete. <see cref="WeChatVideoDownloadStatus.Expired"/> is reported
/// by the status instead of this enum.
/// </summary>
public enum WeChatVideoDownloadFailure
{
    /// <summary>No failure.</summary>
    None,

    /// <summary>The configured output directory does not exist.</summary>
    DirectoryMissing,

    /// <summary>The network or the server failed after the retries.</summary>
    Network,

    /// <summary>The server response did not follow the expected media protocol.</summary>
    Protocol,

    /// <summary>The partial or final file could not be written.</summary>
    FileWrite,
}
