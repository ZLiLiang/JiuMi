namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The outcome of checking the cache for a system proxy left behind by an interrupted capture.
/// </summary>
public enum WeChatVideoResidualRestoreStatus
{
    /// <summary>No capture state was left over.</summary>
    NothingToRestore,

    /// <summary>The stored snapshot was applied and the state file removed.</summary>
    Restored,

    /// <summary>A state file exists but could not be read or applied; the user must be told.</summary>
    Failed,
}

/// <summary>
/// What the startup residual check found and did.
/// </summary>
/// <param name="Status">Whether a leftover proxy was restored.</param>
/// <param name="ProxyServer">The proxy server value that was restored, when known.</param>
/// <param name="Error">The failure that prevented the restore.</param>
public sealed record WeChatVideoResidualRestoreResult(
    WeChatVideoResidualRestoreStatus Status,
    string? ProxyServer = null,
    Exception? Error = null)
{
    /// <summary>
    /// Gets a result stating that there was nothing to restore.
    /// </summary>
    public static WeChatVideoResidualRestoreResult Nothing
    {
        get;
    } = new(WeChatVideoResidualRestoreStatus.NothingToRestore);
}
