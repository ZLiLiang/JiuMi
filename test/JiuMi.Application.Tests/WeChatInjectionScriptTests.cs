using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatInjectionScriptTests
{
    [Fact]
    public void Create_GivenLoopbackReceiver_ReplacesPlaceholder()
    {
        var receiver = new Uri("http://127.0.0.1:51234/capture-token/");

        string script = WeChatInjectionScript.Create(receiver);

        Assert.Contains(receiver.AbsoluteUri, script, StringComparison.Ordinal);
        Assert.DoesNotContain(WeChatInjectionScript.ReceiverPlaceholder, script, StringComparison.Ordinal);
        Assert.Contains("H5ExtTransfer:ok", script, StringComparison.Ordinal);
        Assert.Contains("/web/pages/feed", script, StringComparison.Ordinal);
        Assert.Contains("/web/pages/home", script, StringComparison.Ordinal);
        Assert.Contains("currentSrc", script, StringComparison.Ordinal);
        Assert.Contains("dwellTick", script, StringComparison.Ordinal);
        Assert.Contains("xweb", script, StringComparison.Ordinal);
        Assert.Contains("discover", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://127.0.0.1:51234/capture/")]
    [InlineData("http://example.com/capture/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("/capture/")]
    public void Create_GivenNonLoopbackHttpReceiver_Throws(string url)
    {
        Assert.ThrowsAny<ArgumentException>(() => WeChatInjectionScript.Create(new Uri(url, UriKind.RelativeOrAbsolute)));
    }
}
