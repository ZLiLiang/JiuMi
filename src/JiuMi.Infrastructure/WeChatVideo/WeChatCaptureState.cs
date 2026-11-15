namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// What the on-disk capture state contained.
/// </summary>
internal enum WeChatCaptureStateReadStatus
{
    Missing,
    Valid,
    Invalid,
}

/// <summary>
/// The result of reading the capture state file.
/// </summary>
/// <param name="Status">Whether a file existed and could be read.</param>
/// <param name="State">The parsed state for <see cref="WeChatCaptureStateReadStatus.Valid"/>.</param>
internal sealed record WeChatCaptureStateReadResult(
    WeChatCaptureStateReadStatus Status,
    WeChatCaptureState? State);

/// <summary>
/// The ports and proxy snapshot written before the system proxy is switched.
/// </summary>
/// <param name="ProxyPort">The local MITM proxy port.</param>
/// <param name="ListenerPort">The loopback capture listener port.</param>
/// <param name="StartedAt">When the capture started.</param>
/// <param name="Proxy">The WinINet values to restore after a crash.</param>
internal sealed record WeChatCaptureState(
    int ProxyPort,
    int ListenerPort,
    DateTimeOffset StartedAt,
    WinInetProxySnapshot Proxy);
