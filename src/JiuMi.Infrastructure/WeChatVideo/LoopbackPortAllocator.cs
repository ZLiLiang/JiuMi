using System.Net;
using System.Net.Sockets;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Asks the operating system for an unused loopback port.
/// </summary>
internal static class LoopbackPortAllocator
{
    /// <summary>
    /// Reserves and releases one ephemeral loopback port.
    /// </summary>
    /// <returns>A port number in the ephemeral range.</returns>
    public static int Allocate()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
