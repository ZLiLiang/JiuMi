using JiuMi.Infrastructure.WeChatVideo;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatPacScriptTests
{
    [Fact]
    public void Build_GivenPlainProxy_SendsThePageToTheCapturePortAndTheRestToTheOriginalProxy()
    {
        string script = WeChatPacScript.Build(1, "127.0.0.1:7897", 51234);

        Assert.Contains("host === 'channels.weixin.qq.com'", script, StringComparison.Ordinal);
        Assert.Contains("PROXY 127.0.0.1:51234", script, StringComparison.Ordinal);
        Assert.Contains("PROXY 127.0.0.1:7897", script, StringComparison.Ordinal);
        Assert.DoesNotContain("finder.video.qq.com", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "127.0.0.1:7897")]
    [InlineData(0, "127.0.0.1:7897")]
    [InlineData(1, null)]
    [InlineData(1, "  ")]
    public void Build_GivenNoActiveProxy_FallsBackToDirect(int? proxyEnable, string? proxyServer)
    {
        string script = WeChatPacScript.Build(proxyEnable, proxyServer, 51234);

        Assert.Contains("return 'DIRECT';", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PROXY 127.0.0.1:7897", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_GivenProtocolSpecificProxy_UsesHttpAndHttpsEntries()
    {
        string script = WeChatPacScript.Build(
            1,
            "http=127.0.0.1:8080;https=127.0.0.1:8443",
            51234);

        Assert.Contains("url.substring(0, 5) === 'http:'", script, StringComparison.Ordinal);
        Assert.Contains("PROXY 127.0.0.1:8080", script, StringComparison.Ordinal);
        Assert.Contains("PROXY 127.0.0.1:8443", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_GivenProtocolSpecificProxyWithoutOneProtocol_UsesDirectForIt()
    {
        string script = WeChatPacScript.Build(1, "http=127.0.0.1:8080", 51234);

        Assert.Contains("PROXY 127.0.0.1:8080", script, StringComparison.Ordinal);
        Assert.Contains("return 'DIRECT';", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_GivenUnsafeProxyValue_FallsBackToDirect()
    {
        string script = WeChatPacScript.Build(1, "127.0.0.1:7897'; alert(1); //", 51234);

        Assert.Contains("return 'DIRECT';", script, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(1)", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Build_GivenInvalidCapturePort_Throws(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WeChatPacScript.Build(1, "127.0.0.1:7897", port));
    }
}
