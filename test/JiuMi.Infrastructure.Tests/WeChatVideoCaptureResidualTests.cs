using System.Runtime.Versioning;
using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public sealed class WeChatVideoCaptureResidualTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));

    private readonly InMemoryWinInetRegistry _registry = new();
    private readonly WeChatCaptureStateStore _stateStore;

    public WeChatVideoCaptureResidualTests()
    {
        string configDirectory = Path.Combine(_root, "config");
        Directory.CreateDirectory(configDirectory);
        _stateStore = new WeChatCaptureStateStore(configDirectory, Logger.None);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreResidualAsync_GivenLeftoverState_RestoresProxyAndDeletesState()
    {
        _registry.Set("ProxyEnable", 0);
        _registry.Set("ProxyServer", "http=127.0.0.1:7897;https=127.0.0.1:51234");
        _registry.Set("ProxyOverride", "changed");
        Assert.True(_stateStore.Write(new WeChatCaptureState(
            51234,
            51235,
            DateTimeOffset.UnixEpoch,
            new WinInetProxySnapshot(1, "127.0.0.1:7897", "localhost", null, null))));

        using var capture = CreateCapture();

        WeChatVideoResidualRestoreResult result = await capture.RestoreResidualAsync();

        Assert.Equal(WeChatVideoResidualRestoreStatus.Restored, result.Status);
        Assert.Equal("127.0.0.1:7897", result.ProxyServer);
        Assert.Equal(1, _registry.ReadInt32("ProxyEnable"));
        Assert.Equal("127.0.0.1:7897", _registry.ReadString("ProxyServer"));
        Assert.Equal("localhost", _registry.ReadString("ProxyOverride"));
        Assert.Equal(WeChatCaptureStateReadStatus.Missing, _stateStore.Read().Status);
    }

    [Fact]
    public async Task RestoreResidualAsync_GivenNoState_ReturnsNothing()
    {
        using var capture = CreateCapture();

        WeChatVideoResidualRestoreResult result = await capture.RestoreResidualAsync();

        Assert.Equal(WeChatVideoResidualRestoreStatus.NothingToRestore, result.Status);
    }

    [Fact]
    public async Task RestoreResidualAsync_GivenUnreadableState_ReturnsFailed()
    {
        string directory = Path.Combine(_root, "config", "cache", "wechat");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "state.json"), "{");

        using var capture = CreateCapture();

        WeChatVideoResidualRestoreResult result = await capture.RestoreResidualAsync();

        Assert.Equal(WeChatVideoResidualRestoreStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
    }

    private WeChatVideoCapture CreateCapture()
    {
        var proxy = new WinInetSystemProxy(_registry, () => { });
        return new WeChatVideoCapture(
            _stateStore,
            proxy,
            new WindowsWeChatCertificateStore(TimeProvider.System),
            TimeProvider.System,
            Logger.None);
    }
}
