using System.Runtime.Versioning;
using JiuMi.Infrastructure.WeChatVideo;

namespace JiuMi.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public sealed class WinInetSystemProxyTests
{
    private readonly InMemoryWinInetRegistry _registry = new();
    private int _refreshCount;
    private readonly WinInetSystemProxy _proxy;

    public WinInetSystemProxyTests()
    {
        _proxy = new WinInetSystemProxy(_registry, () => _refreshCount++);
    }

    [Fact]
    public void CaptureSnapshot_GivenValues_ReturnsEachOne()
    {
        _registry.Set("ProxyEnable", 1);
        _registry.Set("ProxyServer", "127.0.0.1:7897");
        _registry.Set("ProxyOverride", "localhost;127.*");
        _registry.Set("AutoConfigURL", "http://pac.example/proxy.pac");
        _registry.Set("AutoDetect", 1);

        WinInetProxySnapshot snapshot = _proxy.CaptureSnapshot();

        Assert.Equal(1, snapshot.ProxyEnable);
        Assert.Equal("127.0.0.1:7897", snapshot.ProxyServer);
        Assert.Equal("localhost;127.*", snapshot.ProxyOverride);
        Assert.Equal("http://pac.example/proxy.pac", snapshot.AutoConfigUrl);
        Assert.Equal(1, snapshot.AutoDetect);
    }

    [Fact]
    public void CaptureSnapshot_GivenAbsentValues_ReturnsNulls()
    {
        WinInetProxySnapshot snapshot = _proxy.CaptureSnapshot();

        Assert.Null(snapshot.ProxyEnable);
        Assert.Null(snapshot.ProxyServer);
        Assert.Null(snapshot.ProxyOverride);
        Assert.Null(snapshot.AutoConfigUrl);
        Assert.Null(snapshot.AutoDetect);
    }

    [Fact]
    public void ApplyHttpsProxy_GivenPlainProxy_KeepsHttpAndPointsHttpsAtTheLocalPort()
    {
        _registry.Set("ProxyEnable", 1);
        _registry.Set("ProxyServer", "127.0.0.1:7897");
        _registry.Set("ProxyOverride", "localhost");
        _registry.Set("AutoConfigURL", "http://pac.example/proxy.pac");
        _registry.Set("AutoDetect", 1);

        _proxy.ApplyHttpsProxy(51234);

        Assert.Equal("http=127.0.0.1:7897;https=127.0.0.1:51234", _registry.ReadString("ProxyServer"));
        Assert.Equal(1, _registry.ReadInt32("ProxyEnable"));
        Assert.Equal("localhost", _registry.ReadString("ProxyOverride"));
        Assert.Null(_registry.ReadString("AutoConfigURL"));
        Assert.Equal(0, _registry.ReadInt32("AutoDetect"));
        Assert.Equal(4, _registry.WriteCount);
        Assert.Equal(1, _refreshCount);
    }

    [Fact]
    public void Restore_GivenSnapshot_RestoresEveryValueExactly()
    {
        _registry.Set("ProxyEnable", 0);
        _registry.Set("ProxyServer", "http=127.0.0.1:7897;https=127.0.0.1:51234");
        _registry.Set("ProxyOverride", "changed");
        _registry.Set("AutoConfigURL", "changed");
        _registry.Set("AutoDetect", 0);

        var snapshot = new WinInetProxySnapshot(
            ProxyEnable: 1,
            ProxyServer: "127.0.0.1:7897",
            ProxyOverride: "localhost",
            AutoConfigUrl: null,
            AutoDetect: null);

        _proxy.Restore(snapshot);

        Assert.Equal(1, _registry.ReadInt32("ProxyEnable"));
        Assert.Equal("127.0.0.1:7897", _registry.ReadString("ProxyServer"));
        Assert.Equal("localhost", _registry.ReadString("ProxyOverride"));
        Assert.Null(_registry.ReadString("AutoConfigURL"));
        Assert.Null(_registry.ReadInt32("AutoDetect"));
        Assert.Equal(1, _refreshCount);
    }
}
