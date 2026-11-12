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
