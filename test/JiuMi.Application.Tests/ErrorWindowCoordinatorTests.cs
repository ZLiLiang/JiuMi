using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorWindowCoordinatorTests
{
    [Fact]
    public void Open_GivenNoWindowShowing_ReturnsRequestWithCopyableDetails()
    {
        var coordinator = new ErrorWindowCoordinator();
        ErrorRecord record = CreateRecord();

        ErrorWindowRequest? request = coordinator.Open(record, canContinue: true, "界面线程出现未处理的错误");

        Assert.NotNull(request);
        Assert.Same(record, request.Record);
        Assert.True(request.CanContinue);
        Assert.Equal(ErrorWindowAction.Continue, request.DefaultAction);
        Assert.Equal(ErrorRecordFormatter.Format(record, "界面线程出现未处理的错误"), request.DetailsText);
        Assert.True(coordinator.IsOpen);
    }

    [Fact]
    public void Open_GivenMainWindowUnavailable_MakesExitTheDefaultAction()
    {
        var coordinator = new ErrorWindowCoordinator();

        ErrorWindowRequest? request = coordinator.Open(CreateRecord(), canContinue: false, "启动失败");

        Assert.NotNull(request);
        Assert.Equal(ErrorWindowAction.Exit, request.DefaultAction);
    }

    [Fact]
    public void Open_GivenWindowAlreadyShowing_ReturnsNullSoTheProcessCanLeave()
    {
        var coordinator = new ErrorWindowCoordinator();
        coordinator.Open(CreateRecord(), canContinue: true, "第一次");

        ErrorWindowRequest? second = coordinator.Open(CreateRecord(), canContinue: false, "第二次");

        Assert.Null(second);
        Assert.True(coordinator.IsOpen);
    }

    [Fact]
    public void Open_GivenWindowWasClosed_ReturnsANewRequest()
    {
        var coordinator = new ErrorWindowCoordinator();
        coordinator.Open(CreateRecord(), canContinue: true, "第一次");

        coordinator.Close();
        ErrorWindowRequest? reopened = coordinator.Open(CreateRecord(), canContinue: false, "第二次");

        Assert.NotNull(reopened);
        Assert.False(reopened.CanContinue);
        Assert.True(coordinator.IsOpen);
    }

    private static ErrorRecord CreateRecord() =>
        new()
        {
            Timestamp = new DateTimeOffset(2026, 10, 24, 9, 30, 0, TimeSpan.FromHours(8)),
            Level = ErrorLevel.Error,
            Code = ErrorCodes.UnexpectedUiThreadException,
            Operation = "Dispatcher.UnhandledException",
        };
}
