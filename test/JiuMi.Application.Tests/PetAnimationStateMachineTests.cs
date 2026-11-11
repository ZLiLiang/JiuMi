using JiuMi.Application.Pet;

namespace JiuMi.Application.Tests;

public sealed class PetAnimationStateMachineTests
{
    private static readonly DateTimeOffset _origin =
        new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_GivenNoInteraction_StartsIdleAtTheFirstFrame()
    {
        (PetAnimationStateMachine machine, _) = CreateMachine();

        Assert.Equal(PetState.Idle, machine.State);
        Assert.Equal(0, machine.FrameIndex);
    }

    [Fact]
    public void Update_GivenIdleFrameDuration_AdvancesOneFrame()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMilliseconds(110));
        machine.Update();

        Assert.Equal(PetState.Idle, machine.State);
        Assert.Equal(1, machine.FrameIndex);
    }

    [Fact]
    public void Update_GivenFullIdleCycle_LoopsBackToTheFirstFrame()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMilliseconds(12 * 110));
        machine.Update();

        Assert.Equal(PetState.Idle, machine.State);
        Assert.Equal(0, machine.FrameIndex);
    }

    [Fact]
    public void Update_GivenBlinkDeadline_PlaysBlinkOnlyAfterTheIdleDelay()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0d);

        time.Advance(TimeSpan.FromSeconds(3.9));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(100));
        machine.Update();
        Assert.Equal(PetState.Blink, machine.State);
        Assert.Equal(0, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(159));
        machine.Update();
        Assert.Equal(PetState.Blink, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(81));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void Update_GivenBlinkEnded_SchedulesTheNextBlinkFourToEightSecondsLater()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0d);

        time.Advance(TimeSpan.FromSeconds(4));
        machine.Update();
        Assert.Equal(PetState.Blink, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(240));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);

        time.Advance(TimeSpan.FromSeconds(3.9));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(100));
        machine.Update();
        Assert.Equal(PetState.Blink, machine.State);
    }

    [Fact]
    public void Update_GivenSlowBlinkRoll_WaitsNearlyEightSeconds()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.999d);

        time.Advance(TimeSpan.FromSeconds(7.99));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(10));
        machine.Update();
        Assert.Equal(PetState.Blink, machine.State);
    }

    [Fact]
    public void Click_GivenIdle_StartsReactAndReturnsToIdle()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.React, effect);
        Assert.Equal(PetState.React, machine.State);
        Assert.Equal(0, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(240));
        machine.Update();

        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void Click_GivenBlink_InterruptsIntoReact()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0d);

        time.Advance(TimeSpan.FromSeconds(4));
        machine.Update();
        Assert.Equal(PetState.Blink, machine.State);

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.React, effect);
        Assert.Equal(PetState.React, machine.State);
    }

    [Fact]
    public void Click_GivenSleep_StartsWakeInsteadOfReact()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.Wake, effect);
        Assert.Equal(PetState.Wake, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(200));
        machine.Update();

        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void Click_GivenSleepIn_StartsWake()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(PetInteractionRules.SleepDelay);
        machine.Update();
        Assert.Equal(PetState.SleepIn, machine.State);

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.Wake, effect);
        Assert.Equal(PetState.Wake, machine.State);
    }

    [Fact]
    public void Click_GivenWalkOut_InterruptsTheEntranceIntoReact()
    {
        (PetAnimationStateMachine machine, _) = CreateMachine();

        machine.BeginEntrance();
        machine.Update();
        Assert.Equal(PetState.WalkOut, machine.State);

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.React, effect);
        Assert.Equal(PetState.React, machine.State);

        machine.CompleteEntrance();
        Assert.Equal(PetState.React, machine.State);
    }

    [Fact]
    public void BeginDrag_GivenAnyAwakeState_EntersDragAndEndDragReturnsIdle()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        machine.BeginDrag();

        Assert.Equal(PetState.Drag, machine.State);
        time.Advance(TimeSpan.FromSeconds(2));
        machine.Update();
        Assert.Equal(PetState.Drag, machine.State);

        machine.EndDrag();

        Assert.Equal(PetState.Idle, machine.State);
        Assert.Equal(0, machine.FrameIndex);
    }

    [Fact]
    public void Click_GivenDrag_IsIgnored()
    {
        (PetAnimationStateMachine machine, _) = CreateMachine();
        machine.BeginDrag();

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.None, effect);
        Assert.Equal(PetState.Drag, machine.State);
    }

    [Fact]
    public void BeginDrag_GivenWalkOut_TakesOverTheEntrance()
    {
        (PetAnimationStateMachine machine, _) = CreateMachine();
        machine.BeginEntrance();

        machine.BeginDrag();

        Assert.Equal(PetState.Drag, machine.State);

        machine.CompleteEntrance();
        Assert.Equal(PetState.Drag, machine.State);
    }

    [Fact]
    public void Update_GivenFiveMinutesIdle_EntersSleepInThenSleep()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        Advance(machine, time, PetInteractionRules.SleepDelay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.Idle, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.SleepIn, machine.State);

        time.Advance(TimeSpan.FromMilliseconds(480));
        machine.Update();
        Assert.Equal(PetState.Sleep, machine.State);
    }

    [Fact]
    public void Update_GivenEntranceInProgress_StartsTheSleepCountdownOnlyAfterItEnds()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        machine.BeginEntrance();

        time.Advance(TimeSpan.FromMinutes(10));
        machine.Update();
        Assert.Equal(PetState.WalkOut, machine.State);

        machine.CompleteEntrance();
        Assert.Equal(PetState.Idle, machine.State);

        Advance(machine, time, PetInteractionRules.SleepDelay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.Idle, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.SleepIn, machine.State);
    }

    [Fact]
    public void Click_GivenFourMinutesIdle_RestartsTheSleepCountdown()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMinutes(4));
        machine.Click();
        time.Advance(TimeSpan.FromMilliseconds(240));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);

        Advance(machine, time, TimeSpan.FromMinutes(4));
        Assert.Equal(PetState.Idle, machine.State);

        Advance(machine, time, TimeSpan.FromMinutes(1));
        Assert.Equal(PetState.SleepIn, machine.State);
    }

    [Fact]
    public void Update_GivenWalkFrameDuration_LoopsWalkFrames()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        machine.BeginEntrance();

        time.Advance(TimeSpan.FromMilliseconds(100));
        machine.Update();
        Assert.Equal(1, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(700));
        machine.Update();
        Assert.Equal(PetState.WalkOut, machine.State);
        Assert.Equal(0, machine.FrameIndex);
    }

    [Fact]
    public void Hide_GivenHidden_StopsTheClockAndShowReturnsToIdle()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        machine.Hide();
        time.Advance(TimeSpan.FromMinutes(10));
        machine.Update();
        Assert.Equal(PetState.Idle, machine.State);

        machine.Show();
        Assert.Equal(PetState.Idle, machine.State);

        time.Advance(PetInteractionRules.SleepDelay);
        machine.Update();
        Assert.Equal(PetState.SleepIn, machine.State);
    }

    [Fact]
    public void RightClick_GivenSleep_WakesThePet()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        machine.RightClick();

        Assert.Equal(PetState.Wake, machine.State);
    }

    [Fact]
    public void DoubleClick_GivenSleep_WakesThePet()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        machine.DoubleClick();

        Assert.Equal(PetState.Wake, machine.State);
    }

    [Fact]
    public void Update_GivenSleep_LoopsAndNeverBlinks()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        time.Advance(TimeSpan.FromMinutes(2));
        machine.Update();

        Assert.Equal(PetState.Sleep, machine.State);
    }

    [Fact]
    public void Hide_GivenSleepAndShow_ReturnsToIdle()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        machine.Hide();
        machine.Show();

        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void Click_GivenWalkOutAfterInterrupt_StartsReact()
    {
        (PetAnimationStateMachine machine, _) = CreateMachine();
        machine.BeginEntrance();

        machine.InterruptEntrance();
        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.React, effect);
        Assert.Equal(PetState.React, machine.State);
    }

    [Fact]
    public void Click_GivenWake_StartsReact()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);
        machine.Click();
        Assert.Equal(PetState.Wake, machine.State);

        PetClickEffect effect = machine.Click();

        Assert.Equal(PetClickEffect.React, effect);
        Assert.Equal(PetState.React, machine.State);
    }

    [Fact]
    public void InterruptEntrance_GivenWalkOut_ReturnsToIdleAndRestartsTheSleepCountdown()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();
        machine.BeginEntrance();

        machine.InterruptEntrance();

        Assert.Equal(PetState.Idle, machine.State);
        Advance(machine, time, PetInteractionRules.SleepDelay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.Idle, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.SleepIn, machine.State);
    }

    [Fact]
    public void DoubleClick_GivenWalkOut_StopsTheEntrance()
    {
        (PetAnimationStateMachine machine, _) = CreateMachine();
        machine.BeginEntrance();

        machine.DoubleClick();

        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void RightClick_GivenFourMinutesIdle_RestartsTheSleepCountdown()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMinutes(4));
        machine.RightClick();
        Advance(machine, time, TimeSpan.FromMinutes(4));
        Assert.NotEqual(PetState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMinutes(1));
        Assert.Equal(PetState.SleepIn, machine.State);
    }

    [Fact]
    public void BeginDrag_GivenFourMinutesIdle_RestartsTheSleepCountdownWhenItEnds()
    {
        (PetAnimationStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMinutes(4));
        machine.BeginDrag();
        Advance(machine, time, TimeSpan.FromMinutes(4));
        Assert.Equal(PetState.Drag, machine.State);

        machine.EndDrag();
        Advance(machine, time, PetInteractionRules.SleepDelay - TimeSpan.FromMilliseconds(1));
        Assert.NotEqual(PetState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(1));
        Assert.Equal(PetState.SleepIn, machine.State);
    }

    private static (PetAnimationStateMachine Machine, ManualTimeProvider Time) CreateMachine(
        double random = 0.5d)
    {
        var time = new ManualTimeProvider(_origin);
        var machine = new PetAnimationStateMachine(time, new FixedRandom(random));
        return (machine, time);
    }

    private static void Sleep(PetAnimationStateMachine machine, ManualTimeProvider time)
    {
        time.Advance(PetInteractionRules.SleepDelay);
        machine.Update();
        time.Advance(TimeSpan.FromMilliseconds(480));
        machine.Update();
        Assert.Equal(PetState.Sleep, machine.State);
    }

    private static void Advance(
        PetAnimationStateMachine machine,
        ManualTimeProvider time,
        TimeSpan duration)
    {
        TimeSpan step = TimeSpan.FromMilliseconds(50);
        TimeSpan remaining = duration;
        while (remaining > TimeSpan.Zero)
        {
            TimeSpan delta = remaining < step ? remaining : step;
            time.Advance(delta);
            machine.Update();
            remaining -= delta;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FixedRandom : Random
    {
        private readonly double _value;

        public FixedRandom(double value)
        {
            _value = value;
        }

        public override double NextDouble() => _value;
    }
}
