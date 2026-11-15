namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// The HKCU WinINet values that have to be restored exactly after a capture.
/// </summary>
/// <param name="ProxyEnable">The <c>ProxyEnable</c> value.</param>
/// <param name="ProxyServer">The <c>ProxyServer</c> value.</param>
/// <param name="ProxyOverride">The <c>ProxyOverride</c> value.</param>
/// <param name="AutoConfigUrl">The <c>AutoConfigURL</c> value.</param>
/// <param name="AutoDetect">The <c>AutoDetect</c> value.</param>
internal sealed record WinInetProxySnapshot(
    int? ProxyEnable,
    string? ProxyServer,
    string? ProxyOverride,
    string? AutoConfigUrl,
    int? AutoDetect);
