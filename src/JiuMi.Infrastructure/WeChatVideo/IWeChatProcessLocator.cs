namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Locates running WeChat processes and identifies the <c>NetworkService</c> subprocess.
/// </summary>
internal interface IWeChatProcessLocator
{
    /// <summary>
    /// Returns the currently running target WeChat processes (<c>Weixin</c> and <c>WeChatAppEx</c>).
    /// </summary>
    /// <returns>The discovered process descriptors.</returns>
    IReadOnlyList<WeChatProcessInfo> FindTargetProcesses();
}
