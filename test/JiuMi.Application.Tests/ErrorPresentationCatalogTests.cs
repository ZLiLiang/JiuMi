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
}
