using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class TitaniumWeChatProxyTests
{
    [Fact]
    public void Start_GivenOurOwnRootCertificate_StartsAndStops()
    {
        using var listener = new WeChatCaptureListener(_ => { }, Logger.None);
        WeChatCaptureListenerStart receiver = listener.Start();
        using var certificate = WeChatRootCertificateFactory.Create(DateTimeOffset.UtcNow);
        using var proxy = new TitaniumWeChatProxy(certificate, receiver.ReceiverUrl, Logger.None);
        int port = LoopbackPortAllocator.Allocate();

        proxy.Start(port);

        proxy.StopSynchronously();
    }
}
