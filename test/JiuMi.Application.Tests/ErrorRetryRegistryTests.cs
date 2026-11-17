using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorRetryRegistryTests
{
    [Fact]
    public void Find_GivenRegisteredHandler_ReturnsIt()
    {
        var registry = new ErrorRetryRegistry();
        Func<Task<bool>> handler = () => Task.FromResult(true);

        registry.Register("WeChatVideo.CaptureStartFailed", handler);

        Assert.Same(handler, registry.Find("WeChatVideo.CaptureStartFailed"));
    }

    [Fact]
    public void Find_GivenUnknownCode_ReturnsNull()
    {
        var registry = new ErrorRetryRegistry();

        Assert.Null(registry.Find("Unknown.Code"));
    }

    [Fact]
    public void Register_GivenTheSameCode_ReplacesTheHandler()
    {
        var registry = new ErrorRetryRegistry();
        Func<Task<bool>> first = () => Task.FromResult(true);
        Func<Task<bool>> second = () => Task.FromResult(false);
        registry.Register("WeChatVideo.CaptureStartFailed", first);

        registry.Register("WeChatVideo.CaptureStartFailed", second);

        Assert.Same(second, registry.Find("WeChatVideo.CaptureStartFailed"));
    }

    [Fact]
    public void Register_GivenBlankCode_Throws()
    {
        var registry = new ErrorRetryRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register("  ", () => Task.FromResult(true)));
    }

    [Fact]
    public void Register_GivenNullHandler_Throws()
    {
        var registry = new ErrorRetryRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register("WeChatVideo.CaptureStartFailed", null!));
    }

    [Fact]
    public void Unregister_GivenRegisteredCode_RemovesIt()
    {
        var registry = new ErrorRetryRegistry();
        registry.Register("WeChatVideo.CaptureStartFailed", () => Task.FromResult(true));

        registry.Unregister("WeChatVideo.CaptureStartFailed");

        Assert.Null(registry.Find("WeChatVideo.CaptureStartFailed"));
    }

    [Fact]
    public void Unregister_GivenUnknownCode_DoesNothing()
    {
        var registry = new ErrorRetryRegistry();

        registry.Unregister("Unknown.Code");

        Assert.Null(registry.Find("Unknown.Code"));
    }
}
