using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoStartupNoticeTests
{
    [Fact]
    public void Take_GivenNothingReported_ReturnsNull()
    {
        var notice = new WeChatVideoStartupNotice();

        Assert.Null(notice.Take());
    }

    [Fact]
    public void Report_GivenRestored_TakeReturnsItOnce()
    {
        var notice = new WeChatVideoStartupNotice();
        var result = new WeChatVideoResidualRestoreResult(
            WeChatVideoResidualRestoreStatus.Restored,
            "http://127.0.0.1:7897");

        notice.Report(result);

        WeChatVideoResidualRestoreResult? taken = notice.Take();
        Assert.NotNull(taken);
        Assert.Equal(WeChatVideoResidualRestoreStatus.Restored, taken.Status);
        Assert.Equal("http://127.0.0.1:7897", taken.ProxyServer);
        Assert.Null(notice.Take());
    }

    [Fact]
    public void Report_GivenFailure_KeepsTheError()
    {
        var notice = new WeChatVideoStartupNotice();
        var error = new InvalidOperationException("proxy could not be restored");
        var result = new WeChatVideoResidualRestoreResult(
            WeChatVideoResidualRestoreStatus.Failed,
            "http://127.0.0.1:7897",
            error);

        notice.Report(result);

        WeChatVideoResidualRestoreResult? taken = notice.Take();
        Assert.NotNull(taken);
        Assert.Equal(WeChatVideoResidualRestoreStatus.Failed, taken.Status);
        Assert.Same(error, taken.Error);
    }

    [Fact]
    public void Report_GivenASecondResult_KeepsTheFirst()
    {
        var notice = new WeChatVideoStartupNotice();
        var first = new WeChatVideoResidualRestoreResult(WeChatVideoResidualRestoreStatus.Restored);
        var second = new WeChatVideoResidualRestoreResult(WeChatVideoResidualRestoreStatus.Failed);

        notice.Report(first);
        notice.Report(second);

        WeChatVideoResidualRestoreResult? taken = notice.Take();
        Assert.NotNull(taken);
        Assert.Equal(WeChatVideoResidualRestoreStatus.Restored, taken.Status);
    }

    [Fact]
    public void Report_GivenNull_Throws()
    {
        var notice = new WeChatVideoStartupNotice();

        Assert.Throws<ArgumentNullException>(() => notice.Report(null!));
    }
}
