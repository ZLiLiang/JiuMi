using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

/// <summary>
/// The ownership transitions between the page and the desktop: walk-out, complete, cancel and the
/// return path, including the guards that keep a sleeping or hidden cat at home.
/// </summary>
public sealed class MyJiuMiPresenceMachineTests
{
    [Fact]
    public void NewMachine_StartsAtHome()
    {
        var machine = new MyJiuMiPresenceMachine();

        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
        Assert.False(machine.IsTransitioning);
    }

    [Theory]
    [InlineData(MyJiuMiLeaveTrigger.HiddenToTray)]
    [InlineData(MyJiuMiLeaveTrigger.Minimized)]
    public void TryBeginLeave_GivenAValidWalkOut_EntersLeaving(MyJiuMiLeaveTrigger trigger)
    {
        var machine = new MyJiuMiPresenceMachine();

        bool started = machine.TryBeginLeave(
            trigger,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true);

        Assert.True(started);
        Assert.Equal(MyJiuMiPresence.Leaving, machine.Presence);
        Assert.True(machine.IsTransitioning);
    }

    [Theory]
    [InlineData(MyJiuMiLeaveTrigger.Exiting)]
    [InlineData(MyJiuMiLeaveTrigger.PageLeft)]
    public void TryBeginLeave_GivenANonWalkOutTrigger_StaysHome(MyJiuMiLeaveTrigger trigger)
    {
        var machine = new MyJiuMiPresenceMachine();

        bool started = machine.TryBeginLeave(
            trigger,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true);

        Assert.False(started);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryBeginLeave_GivenASleepingCat_StaysHome()
    {
        var machine = new MyJiuMiPresenceMachine();

        bool started = machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.HiddenToTray,
            pageCurrent: true,
            catAwake: false,
            desktopAvailable: true);

        Assert.False(started);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryBeginLeave_GivenAnotherPage_StaysHome()
    {
        var machine = new MyJiuMiPresenceMachine();

        bool started = machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.Minimized,
            pageCurrent: false,
            catAwake: true,
            desktopAvailable: true);

        Assert.False(started);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryBeginLeave_GivenNoDesktopHost_StaysHome()
    {
        var machine = new MyJiuMiPresenceMachine();

        bool started = machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.Minimized,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: false);

        Assert.False(started);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryBeginLeave_GivenADraggedOutSleepingCat_EntersLeaving()
    {
        var machine = new MyJiuMiPresenceMachine();

        bool started = machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.DraggedOut,
            pageCurrent: true,
            catAwake: false,
            desktopAvailable: true);

        Assert.True(started);
        Assert.Equal(MyJiuMiPresence.Leaving, machine.Presence);
    }

    [Fact]
    public void TryCancelLeave_GivenAFailedDragOutHandoff_ReturnsHome()
    {
        var machine = new MyJiuMiPresenceMachine();
        Assert.True(machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.DraggedOut,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true));

        bool cancelled = machine.TryCancelLeave();

        Assert.True(cancelled);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryBeginLeave_WhileLeaving_IgnoresRepeatedRequests()
    {
        var machine = new MyJiuMiPresenceMachine();
        Assert.True(machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.HiddenToTray,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true));

        bool repeated = machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.Minimized,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true);

        Assert.False(repeated);
        Assert.Equal(MyJiuMiPresence.Leaving, machine.Presence);
    }

    [Fact]
    public void TryCompleteLeave_GivenLeaving_EntersDesktop()
    {
        var machine = CreateLeavingMachine();

        bool completed = machine.TryCompleteLeave();

        Assert.True(completed);
        Assert.Equal(MyJiuMiPresence.Desktop, machine.Presence);
        Assert.False(machine.IsTransitioning);
    }

    [Fact]
    public void TryCancelLeave_GivenLeaving_ReturnsHome()
    {
        var machine = CreateLeavingMachine();

        bool cancelled = machine.TryCancelLeave();

        Assert.True(cancelled);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryCompleteLeave_GivenHome_FailsWithoutChangingThePresence()
    {
        var machine = new MyJiuMiPresenceMachine();

        Assert.False(machine.TryCompleteLeave());
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryBeginReturn_GivenDesktopAndAReachablePage_EntersReturning()
    {
        MyJiuMiPresenceMachine machine = CreateDesktopMachine();

        bool started = machine.TryBeginReturn(pageReachable: true);

        Assert.True(started);
        Assert.Equal(MyJiuMiPresence.Returning, machine.Presence);
        Assert.True(machine.IsTransitioning);
    }

    [Fact]
    public void TryBeginReturn_GivenAnUnreachablePage_StaysOnDesktop()
    {
        MyJiuMiPresenceMachine machine = CreateDesktopMachine();

        bool started = machine.TryBeginReturn(pageReachable: false);

        Assert.False(started);
        Assert.Equal(MyJiuMiPresence.Desktop, machine.Presence);
    }

    [Fact]
    public void TryCompleteReturn_GivenReturning_EntersHome()
    {
        MyJiuMiPresenceMachine machine = CreateDesktopMachine();
        Assert.True(machine.TryBeginReturn(pageReachable: true));

        bool completed = machine.TryCompleteReturn();

        Assert.True(completed);
        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
    }

    [Fact]
    public void TryAbortReturn_GivenReturning_StaysOnDesktop()
    {
        MyJiuMiPresenceMachine machine = CreateDesktopMachine();
        Assert.True(machine.TryBeginReturn(pageReachable: true));

        bool aborted = machine.TryAbortReturn();

        Assert.True(aborted);
        Assert.Equal(MyJiuMiPresence.Desktop, machine.Presence);
    }

    [Fact]
    public void Reset_GivenDesktop_ReturnsHome()
    {
        MyJiuMiPresenceMachine machine = CreateDesktopMachine();

        machine.Reset();

        Assert.Equal(MyJiuMiPresence.Home, machine.Presence);
        Assert.False(machine.IsTransitioning);
    }

    private static MyJiuMiPresenceMachine CreateLeavingMachine()
    {
        var machine = new MyJiuMiPresenceMachine();
        Assert.True(machine.TryBeginLeave(
            MyJiuMiLeaveTrigger.HiddenToTray,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true));
        return machine;
    }

    private static MyJiuMiPresenceMachine CreateDesktopMachine()
    {
        MyJiuMiPresenceMachine machine = CreateLeavingMachine();
        Assert.True(machine.TryCompleteLeave());
        return machine;
    }
}
