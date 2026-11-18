using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorPresentationCatalogTests
{
    [Theory]
    [InlineData(ErrorCodes.StartupTimeout, ErrorLevel.Warning, ErrorCarrier.InfoBar)]
    [InlineData(ErrorCodes.StartupFaulted, ErrorLevel.Error, ErrorCarrier.ErrorWindow)]
    [InlineData(ErrorCodes.ShellMainWindowFailed, ErrorLevel.Error, ErrorCarrier.ErrorWindow)]
    public void Find_GivenStartupCode_ReturnsTheRegisteredPresentation(
        string code,
        ErrorLevel level,
        ErrorCarrier carrier)
    {
        ErrorPresentation? presentation = ErrorPresentationCatalog.Find(code);

        Assert.NotNull(presentation);
        Assert.Equal(level, presentation.Level);
        Assert.Equal(carrier, presentation.Carrier);
    }

    [Fact]
    public void Find_GivenDiagnosticsExportFailed_OffersTheRetryAction()
    {
        ErrorPresentation? presentation = ErrorPresentationCatalog.Find(ErrorCodes.DiagnosticsExportFailed);

        Assert.NotNull(presentation);
        Assert.Equal(ErrorLevel.Error, presentation.Level);
        Assert.Equal(ErrorCarrier.InfoBar, presentation.Carrier);
        Assert.Equal(ErrorNoticeAction.Retry, presentation.Action);
    }

    [Theory]
    [InlineData("WeChatVideo.CaptureStartFailed", ErrorCarrier.InfoBar, ErrorNoticeAction.Retry)]
    [InlineData("WeChatVideo.CertificateUninstallFailed", ErrorCarrier.InfoBar, ErrorNoticeAction.Retry)]
    [InlineData("WeChatVideo.ProxyRestoreFailed", ErrorCarrier.ErrorWindow, ErrorNoticeAction.None)]
    public void Find_GivenWeChatVideoCode_ReturnsTheRegisteredPresentation(
        string code,
        ErrorCarrier carrier,
        ErrorNoticeAction action)
    {
        ErrorPresentation? presentation = ErrorPresentationCatalog.Find(code);

        Assert.NotNull(presentation);
        Assert.Equal(ErrorLevel.Error, presentation.Level);
        Assert.Equal(carrier, presentation.Carrier);
        Assert.Equal(action, presentation.Action);
    }
}
