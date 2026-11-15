using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatHtmlInjectorTests
{
    [Fact]
    public void Inject_GivenBodyTag_InsertsBeforeIt()
    {
        string html = "<html><body><h1>Hello</h1></body></html>";

        string injected = WeChatHtmlInjector.Inject(html, "script()");

        Assert.Contains(WeChatHtmlInjector.Marker, injected, StringComparison.Ordinal);
        Assert.True(injected.IndexOf("script()", StringComparison.Ordinal) <
            injected.IndexOf("</body>", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("<html><body><h1>Hello</h1><script>", injected, StringComparison.Ordinal);
        Assert.EndsWith("</script></body></html>", injected, StringComparison.Ordinal);
    }

    [Fact]
    public void Inject_GivenMarkerAlreadyPresent_ReturnsOriginal()
    {
        string html = "<html><body><script>/*" + WeChatHtmlInjector.Marker + "*/</script></body></html>";

        Assert.Same(html, WeChatHtmlInjector.Inject(html, "script()"));
    }

    [Fact]
    public void Inject_GivenNoBodyTag_AppendsScript()
    {
        string html = "<html><h1>Hello</h1></html>";

        string injected = WeChatHtmlInjector.Inject(html, "script()");

        Assert.StartsWith(html, injected, StringComparison.Ordinal);
        Assert.EndsWith("</script>", injected, StringComparison.Ordinal);
    }

    [Fact]
    public void Inject_GivenUppercaseBodyTag_InsertsBeforeIt()
    {
        string html = "<HTML><BODY>x</BODY></HTML>";

        string injected = WeChatHtmlInjector.Inject(html, "script()");

        Assert.True(injected.IndexOf("script()", StringComparison.Ordinal) <
            injected.IndexOf("</BODY>", StringComparison.OrdinalIgnoreCase));
    }
}
