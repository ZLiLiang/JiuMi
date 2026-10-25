using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorWindowTextTests
{
    [Fact]
    public void Labels_MatchTheEmbeddedChineseCopy()
    {
        Assert.Equal("JiuMi", ErrorWindowText.AppTitle);
        Assert.Equal("复制详情", ErrorWindowText.CopyDetails);
        Assert.Equal("已复制", ErrorWindowText.Copied);
        Assert.Equal("查看详情", ErrorWindowText.DetailsHeader);
        Assert.Equal("退出", ErrorWindowText.Exit);
        Assert.Equal("继续运行", ErrorWindowText.Continue);
    }

    [Fact]
    public void MessageFor_GivenUiThreadExceptionCodeAndContinuing_OffersContinuing()
    {
        ErrorWindowMessage message = ErrorWindowText.MessageFor(
            ErrorCodes.UnexpectedUiThreadException,
            canContinue: true);

        Assert.Equal("发生未处理的错误", message.Headline);
        Assert.Equal(
            "界面线程上出现了未处理的错误，但应用仍可继续使用。可以复制详情反馈给开发者，或继续运行。",
            message.Body);
    }

    [Fact]
    public void MessageFor_GivenUiThreadExceptionCodeAndBlocking_OmitsContinuing()
    {
        ErrorWindowMessage message = ErrorWindowText.MessageFor(
            ErrorCodes.UnexpectedUiThreadException,
            canContinue: false);

        Assert.Equal("发生未处理的错误", message.Headline);
        Assert.Equal(
            "界面线程上出现了未处理的错误，应用无法继续。可以复制详情反馈给开发者，然后退出。",
            message.Body);
    }

    [Fact]
    public void MessageFor_GivenUnregisteredCode_FallsBackToTheGenericMessage()
    {
        ErrorWindowMessage message = ErrorWindowText.MessageFor("Unknown.Code", canContinue: false);

        Assert.Equal("发生了未预期的错误", message.Headline);
        Assert.Equal("应用遇到了未预期的问题。可以复制详情反馈给开发者，然后退出。", message.Body);
    }
}
