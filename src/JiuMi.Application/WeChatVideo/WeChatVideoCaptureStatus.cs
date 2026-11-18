namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The session-only state of the WeChat video capture.
/// </summary>
public enum WeChatVideoCaptureStatus
{
    /// <summary>Nothing is captured and the system proxy is untouched.</summary>
    Stopped,

    /// <summary>The certificate, listener, proxy and system proxy are being brought up.</summary>
    Starting,

    /// <summary>The system PAC sends the WeChat page host to the local capture proxy.</summary>
    Running,

    /// <summary>The local proxy is being torn down and the system proxy restored.</summary>
    Stopping,
}
