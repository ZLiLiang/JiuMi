namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Identifies a running WeChat process relevant to read-only memory capture.
/// </summary>
/// <param name="ProcessId">The operating system process identifier.</param>
/// <param name="ProcessName">The executable name without extension.</param>
/// <param name="IsNetworkService">
/// <see langword="true"/> when this process is the Chromium <c>network.mojom.NetworkService</c> utility subprocess.
/// </param>
internal sealed record WeChatProcessInfo(
    int ProcessId,
    string ProcessName,
    bool IsNetworkService);
