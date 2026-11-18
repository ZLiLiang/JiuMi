using JiuMi.Application.MyJiuMi;
using static JiuMi.Application.Tests.MyJiuMiTestTimeline;

namespace JiuMi.Application.Tests;

/// <summary>
/// The desktop-mode additions to the cat state machine: the walk-out walk, the awake guard, and the
/// switch that stops autonomous wandering while the cat lives on the desktop.
/// </summary>
public sealed class MyJiuMiStateMachineDesktopTests
{
    private const double DefaultWidth = 800;

    private static readonly DateTimeOffset _origin =
        new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WalkTo_GivenAnIdleCat_StartsWalkingAtOnce()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);

        bool started = machine.WalkTo(0);

        Assert.True(started);
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.False(machine.IsFacingRight);

        Advance(machine, time, TimeSpan.FromSeconds(1));
        Assert.True(machine.PositionX < 0.5);
    }

    [Fact]
    public void WalkTo_GivenATargetToTheRight_FacesRight()
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine(positionX: 0.2);

        Assert.True(machine.WalkTo(0.9));

        Assert.True(machine.IsFacingRight);
        Assert.Equal(MyJiuMiState.Walk, machine.State);
    }

    [Fact]
    public void WalkTo_GivenASleepingCat_IsRejected()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);

        bool started = machine.WalkTo(1);

        Assert.False(started);
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }

    [Fact]
    public void WalkTo_GivenADraggedCat_IsRejected()
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine();
        machine.BeginDrag();

        bool started = machine.WalkTo(1);

        Assert.False(started);
        Assert.Equal(MyJiuMiState.Drag, machine.State);
    }

    [Fact]
    public void WalkTo_GivenTheTargetReached_ReturnsToLyingIdle()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        double target = 0.5 + (MyJiuMiInteractionRules.WalkSpeed / DefaultWidth);
        Assert.True(machine.WalkTo(target));

        Advance(machine, time, TimeSpan.FromSeconds(2));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(target, machine.PositionX, 6);
    }

    [Fact]
    public void IsAwake_GivenTheSleepStates_IsFalse()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Assert.True(machine.IsAwake);

        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Advance(machine, time, TimeSpan.FromMilliseconds(450));

        Assert.False(machine.IsAwake);
    }

    [Fact]
    public void IsAwake_GivenTheIdleReactAndDragStates_IsTrue()
    {
        (MyJiuMiStateMachine idle, _) = CreateMachine();
        Assert.Equal(MyJiuMiState.LyingIdle, idle.State);
        Assert.True(idle.IsAwake);

        (MyJiuMiStateMachine reacting, _) = CreateMachine();
        reacting.Click();
        Assert.Equal(MyJiuMiState.React, reacting.State);
        Assert.True(reacting.IsAwake);

        (MyJiuMiStateMachine dragging, _) = CreateMachine();
        dragging.BeginDrag();
        Assert.Equal(MyJiuMiState.Drag, dragging.State);
        Assert.True(dragging.IsAwake);
    }

    [Fact]
    public void Update_GivenWanderingDisabled_StaysIdle()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0);
        machine.SetWanderingEnabled(false);

        Advance(machine, time, TimeSpan.FromSeconds(60));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void SetWanderingEnabled_GivenReenabled_SchedulesWanderingAgain()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0);
        machine.SetWanderingEnabled(false);
        Advance(machine, time, TimeSpan.FromSeconds(30));

        machine.SetWanderingEnabled(true);
        bool walked = false;
        for (int step = 0; step < 600 && !walked; step++)
        {
            time.Advance(TimeSpan.FromMilliseconds(50));
            machine.Update();
            walked = machine.State == MyJiuMiState.Walk;
        }

        Assert.True(walked);
    }

    [Fact]
    public void Summon_GivenASleepingDesktopCat_WakesAtTheEntryEdgeThenWalksToTheClampedTarget()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);

        machine.SetPosition(MyJiuMiTransitionRules.SummonEntryEdge(
            new MyJiuMiScreenRect(0, 0, 400, 300),
            desktopCatX: 100));
        machine.Summon(2);

        Assert.Equal(MyJiuMiState.Wake, machine.State);
        Assert.Equal(0, machine.PositionX, 9);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.True(machine.IsFacingRight);

        Advance(machine, time, TimeSpan.FromSeconds(10));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(1, machine.PositionX, 9);
    }

    [Fact]
    public void BeginSummonDesktopWalk_GivenAnIdleCat_WalksInPlaceFacingTheRequestedSide()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);

        Assert.True(machine.BeginSummonDesktopWalk(facingRight: true));

        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.True(machine.IsFacingRight);

        Advance(machine, time, TimeSpan.FromSeconds(2));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.Equal(0.5, machine.PositionX, 9);
    }

    [Fact]
    public void BeginSummonDesktopWalk_GivenTheSummonPace_PlaysTheFastWalkFrames()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: true));
        Assert.Equal(0, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(16));
        machine.Update();
        Assert.Equal(1, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(16));
        machine.Update();
        Assert.Equal(2, machine.FrameIndex);
    }

    [Fact]
    public void WalkTo_GivenTheNormalPace_KeepsTheTwentySixMillisecondFrames()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Assert.True(machine.WalkTo(0.9));

        time.Advance(TimeSpan.FromMilliseconds(16));
        machine.Update();

        Assert.Equal(0, machine.FrameIndex);
    }

    [Fact]
    public void BeginSummonDesktopWalk_GivenASleepingCat_WakesFirstThenWalksInPlace()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Sleep(machine, time);
        double start = machine.PositionX;

        Assert.True(machine.BeginSummonDesktopWalk(facingRight: false));
        Assert.Equal(MyJiuMiState.Wake, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.False(machine.IsFacingRight);
        Assert.Equal(start, machine.PositionX, 9);
    }

    [Fact]
    public void CancelSummonDesktopWalk_GivenAHeldWalk_StopsInPlace()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: false));

        machine.CancelSummonDesktopWalk();
        Advance(machine, time, TimeSpan.FromSeconds(1));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0.5, machine.PositionX, 9);
    }

    [Fact]
    public void CancelSummonDesktopWalk_GivenAWakingCat_SettlesIdleAfterTheWake()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Sleep(machine, time);
        double start = machine.PositionX;
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: false));

        machine.CancelSummonDesktopWalk();
        Advance(machine, time, TimeSpan.FromMilliseconds(450));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(start, machine.PositionX, 9);
    }

    [Fact]
    public void SummonWalkTo_GivenTheHandoffPosition_MovesAtTheSummonSpeed()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: true));
        machine.SetPosition(0.125);

        Assert.True(machine.SummonWalkTo(0.5));
        Advance(machine, time, TimeSpan.FromSeconds(1));

        double expected = 0.125 + (MyJiuMiInteractionRules.SummonWalkSpeed / DefaultWidth);
        Assert.Equal(expected, machine.PositionX, 6);
    }

    [Fact]
    public void SummonWalkTo_GivenAHeldWalk_KeepsTheFastFrameCycle()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: true));
        time.Advance(TimeSpan.FromMilliseconds(16));
        machine.Update();
        Assert.Equal(1, machine.FrameIndex);

        machine.SetPosition(0.125);
        Assert.True(machine.SummonWalkTo(0.5));

        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.Equal(1, machine.FrameIndex);
    }

    [Fact]
    public void SummonWalkTo_GivenTheCurrentPosition_SettlesIdle()
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine(positionX: 0.5);
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: true));
        machine.SetPosition(0.25);

        Assert.False(machine.SummonWalkTo(0.25));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0.25, machine.PositionX, 9);
    }

    [Fact]
    public void WalkTo_AfterASummonWalk_UsesTheNormalSpeedAgain()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(positionX: 0.5);
        Assert.True(machine.BeginSummonDesktopWalk(facingRight: true));
        machine.CancelSummonDesktopWalk();

        Assert.True(machine.WalkTo(0.9));
        Advance(machine, time, TimeSpan.FromSeconds(1));

        double expected = 0.5 + (MyJiuMiInteractionRules.WalkSpeed / DefaultWidth);
        Assert.Equal(expected, machine.PositionX, 6);
    }

    private static void Sleep(MyJiuMiStateMachine machine, ManualTimeProvider time)
    {
        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }

    private static (MyJiuMiStateMachine Machine, ManualTimeProvider Time) CreateMachine(
        double random = 0.25,
        double positionX = 0.5)
    {
        var time = new ManualTimeProvider(_origin);
        var machine = new MyJiuMiStateMachine(time, new FixedRandom(random));
        machine.Activate(DefaultWidth, positionX);
        return (machine, time);
    }
}
