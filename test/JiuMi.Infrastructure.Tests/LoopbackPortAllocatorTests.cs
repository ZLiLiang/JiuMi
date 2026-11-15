using JiuMi.Infrastructure.WeChatVideo;

namespace JiuMi.Infrastructure.Tests;

public sealed class LoopbackPortAllocatorTests
{
    [Fact]
    public void Allocate_ReturnsValidDistinctLoopbackPorts()
    {
        var ports = new HashSet<int>();
        for (int index = 0; index < 16; index++)
        {
            int port = LoopbackPortAllocator.Allocate();
            Assert.InRange(port, 1, 65535);
            ports.Add(port);
        }

        Assert.True(ports.Count > 1, "The allocator returned the same port every time.");
    }
}
