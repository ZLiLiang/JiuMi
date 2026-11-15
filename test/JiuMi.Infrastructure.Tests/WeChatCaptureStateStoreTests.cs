using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatCaptureStateStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));

    public WeChatCaptureStateStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Write_GivenState_RoundTripsPortsAndSnapshot()
    {
        WeChatCaptureStateStore store = CreateStore();
        var startedAt = new DateTimeOffset(2026, 11, 15, 10, 30, 0, TimeSpan.FromHours(8));
        var state = new WeChatCaptureState(
            51234,
            51235,
            startedAt,
            new WinInetProxySnapshot(1, "127.0.0.1:7897", "localhost", null, null));

        Assert.True(store.Write(state));

        WeChatCaptureStateReadResult result = store.Read();
        Assert.Equal(WeChatCaptureStateReadStatus.Valid, result.Status);
        Assert.NotNull(result.State);
        Assert.Equal(51234, result.State.ProxyPort);
        Assert.Equal(51235, result.State.ListenerPort);
        Assert.Equal(startedAt, result.State.StartedAt);
        Assert.Equal(1, result.State.Proxy.ProxyEnable);
        Assert.Equal("127.0.0.1:7897", result.State.Proxy.ProxyServer);
        Assert.Equal("localhost", result.State.Proxy.ProxyOverride);
        Assert.Null(result.State.Proxy.AutoConfigUrl);
        Assert.Null(result.State.Proxy.AutoDetect);
    }

    [Fact]
    public void Read_GivenNoFile_ReturnsMissing()
    {
        Assert.Equal(WeChatCaptureStateReadStatus.Missing, CreateStore().Read().Status);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("""{ "proxyPort": 0, "listenerPort": 1, "startedAt": "2026-11-15T10:30:00.0000000+08:00", "proxy": {} }""")]
    [InlineData("""{ "proxyPort": 1, "listenerPort": 65536, "startedAt": "2026-11-15T10:30:00.0000000+08:00", "proxy": {} }""")]
    [InlineData("""{ "proxyPort": 1, "listenerPort": 2, "startedAt": "not a time", "proxy": {} }""")]
    [InlineData("""{ "proxyPort": 1, "listenerPort": 2, "startedAt": "2026-11-15T10:30:00.0000000+08:00" }""")]
    public void Read_GivenInvalidFile_ReturnsInvalid(string json)
    {
        string directory = Path.Combine(_root, "config", "cache", "wechat");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "state.json"), json);

        Assert.Equal(WeChatCaptureStateReadStatus.Invalid, CreateStore().Read().Status);
    }

    [Fact]
    public void Delete_GivenStateFile_RemovesIt()
    {
        WeChatCaptureStateStore store = CreateStore();
        Assert.True(store.Write(new WeChatCaptureState(
            1,
            2,
            DateTimeOffset.UnixEpoch,
            new WinInetProxySnapshot(null, null, null, null, null))));

        Assert.True(store.Delete());
        Assert.Equal(WeChatCaptureStateReadStatus.Missing, store.Read().Status);
    }

    [Fact]
    public void Write_GivenNoDirectory_ReturnsFalse()
    {
        var store = new WeChatCaptureStateStore(cacheDirectory: null, Logger.None);

        Assert.False(store.Write(new WeChatCaptureState(
            1,
            2,
            DateTimeOffset.UnixEpoch,
            new WinInetProxySnapshot(null, null, null, null, null))));
        Assert.Equal(WeChatCaptureStateReadStatus.Missing, store.Read().Status);
    }

    private WeChatCaptureStateStore CreateStore() => new(Path.Combine(_root, "config"), Logger.None);
}
