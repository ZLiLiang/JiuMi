using JiuMi.Application.MyJiuMi;
using static JiuMi.Application.Tests.MyJiuMiTestTimeline;

namespace JiuMi.Application.Tests;

/// <summary>
/// Behaviour of the My JiuMi cat: frame timing, the sleep countdown, blink and wander scheduling,
/// summoning, dragging and the normalized floor position.
/// </summary>
public sealed class MyJiuMiStateMachineTests
{
    private const double DefaultWidth = 800;

    private static readonly DateTimeOffset _origin =
        new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activate_GivenARoom_StartsLyingIdleAtTheSavedPosition()
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine(positionX: 0.3);

        Assert.True(machine.IsActive);
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0, machine.FrameIndex);
        Assert.Equal(0.3, machine.PositionX, 9);
        Assert.False(machine.IsFacingRight);
    }

    [Fact]
    public void Update_GivenTheIdleFrameDuration_AdvancesOneFrame()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMilliseconds(1500));
        machine.Update();

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(1, machine.FrameIndex);
    }

    [Fact]
    public void Update_GivenAFullIdleCycle_LoopsToTheFirstFrame()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        time.Advance(TimeSpan.FromMilliseconds(3000));
        machine.Update();

        Assert.Equal(0, machine.FrameIndex);
    }

    [Fact]
    public void Update_GivenTheBlinkDelay_PlaysBlinkAndReturnsToLyingIdle()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.25);

        Advance(machine, time, TimeSpan.FromMilliseconds(5500));
        Assert.Equal(MyJiuMiState.Blink, machine.State);
        Assert.Equal(0, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(60));
        machine.Update();
        Assert.Equal(1, machine.FrameIndex);

        time.Advance(TimeSpan.FromMilliseconds(90));
        machine.Update();
        Assert.Equal(2, machine.FrameIndex);

        Advance(machine, time, TimeSpan.FromMilliseconds(60));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void Update_GivenTheFastestBlinkRoll_WaitsFiveSeconds()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0);

        Advance(machine, time, TimeSpan.FromMilliseconds(4990));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(10));
        Assert.Equal(MyJiuMiState.Blink, machine.State);
    }

    [Fact]
    public void Update_GivenTheSlowestBlinkRoll_WaitsSevenSeconds()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.999);

        Advance(machine, time, TimeSpan.FromMilliseconds(6990));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(10));
        Assert.Equal(MyJiuMiState.Blink, machine.State);
    }

    [Fact]
    public void Click_GivenLyingIdle_ReactsWithAHeartAndReturnsToIdle()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        MyJiuMiClickEffect effect = machine.Click();

        Assert.Equal(MyJiuMiClickEffect.React, effect);
        Assert.Equal(MyJiuMiState.React, machine.State);
        Assert.Equal(0, machine.FrameIndex);

        Advance(machine, time, TimeSpan.FromMilliseconds(240));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void Click_GivenBlink_InterruptsIntoReact()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.25);
        Advance(machine, time, TimeSpan.FromMilliseconds(5500));
        Assert.Equal(MyJiuMiState.Blink, machine.State);

        MyJiuMiClickEffect effect = machine.Click();

        Assert.Equal(MyJiuMiClickEffect.React, effect);
        Assert.Equal(MyJiuMiState.React, machine.State);
    }

    [Fact]
    public void Click_GivenSleep_WakesWithoutAHeart()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        MyJiuMiClickEffect effect = machine.Click();

        Assert.Equal(MyJiuMiClickEffect.Wake, effect);
        Assert.Equal(MyJiuMiState.Wake, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void Click_GivenSleepIn_ReactsInsteadOfWaking()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);

        MyJiuMiClickEffect effect = machine.Click();

        Assert.Equal(MyJiuMiClickEffect.React, effect);
        Assert.Equal(MyJiuMiState.React, machine.State);
    }

    [Fact]
    public void Click_GivenDrag_IsIgnored()
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine();
        machine.BeginDrag();

        MyJiuMiClickEffect effect = machine.Click();

        Assert.Equal(MyJiuMiClickEffect.None, effect);
        Assert.Equal(MyJiuMiState.Drag, machine.State);
    }

    [Fact]
    public void Click_GivenWalk_StopsWalkingAndReacts()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        machine.Summon(0.1);
        Advance(machine, time, TimeSpan.FromMilliseconds(480));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        double walkingPosition = machine.PositionX;

        MyJiuMiClickEffect effect = machine.Click();

        Assert.Equal(MyJiuMiClickEffect.React, effect);
        Assert.Equal(walkingPosition, machine.PositionX, 9);
        Assert.Equal(MyJiuMiState.React, machine.State);
    }

    [Fact]
    public void BeginDrag_GivenSleep_PicksTheCatUpDirectly()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        machine.BeginDrag();

        Assert.Equal(MyJiuMiState.Drag, machine.State);

        machine.EndDrag();
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void Update_GivenFiveMinutesWithoutInteraction_Sleeps()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);

        time.Advance(TimeSpan.FromMinutes(5));
        machine.Update();
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }

    [Fact]
    public void Update_GivenASleepDeadlineThatPassesWhileBlinking_FallsAsleepWithoutBlinking()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0);

        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);

        Assert.Equal(MyJiuMiState.SleepIn, machine.State);
    }

    [Fact]
    public void Click_GivenFourMinutesIdle_RestartsTheSleepCountdown()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        Advance(machine, time, TimeSpan.FromMinutes(4));
        machine.Click();
        Advance(machine, time, TimeSpan.FromMilliseconds(240));

        Advance(machine, time, TimeSpan.FromMinutes(4));
        Assert.NotEqual(MyJiuMiState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMinutes(1));
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);
    }

    [Fact]
    public void Summon_GivenAnAwakeCat_StretchesThenWalksToTheTarget()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        machine.Summon(0.25);
        Assert.Equal(MyJiuMiState.Stretch, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(480));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.False(machine.IsFacingRight);

        Advance(machine, time, TimeSpan.FromSeconds(3));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0.25, machine.PositionX, 6);
    }

    [Fact]
    public void Summon_GivenASleepingCat_WakesThenWalksFacingRight()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();
        Sleep(machine, time);

        machine.Summon(0.8);
        Assert.Equal(MyJiuMiState.Wake, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.True(machine.IsFacingRight);

        Advance(machine, time, TimeSpan.FromSeconds(4));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0.8, machine.PositionX, 6);
    }

    [Fact]
    public void Summon_GivenAnOutOfRangeTarget_ClampsTheWalkToTheRoomEdge()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        machine.Summon(2);
        Advance(machine, time, TimeSpan.FromSeconds(5));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(1, machine.PositionX, 9);
    }

    [Fact]
    public void Click_GivenAnInactivePage_IsIgnored()
    {
        var time = new ManualTimeProvider(_origin);
        var machine = new MyJiuMiStateMachine(time, new FixedRandom(0.25));

        Assert.Equal(MyJiuMiClickEffect.None, machine.Click());

        machine.BeginDrag();
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void Update_GivenAQuietCat_WandersAfterTheDecisionAndKeepsTheTargetBounds()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.25);

        Advance(machine, time, TimeSpan.FromSeconds(24));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(6));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        double moved = Math.Abs(machine.PositionX - 0.5) * DefaultWidth;
        Assert.True(moved >= MyJiuMiInteractionRules.WanderMinDistance - 1, $"moved {moved} DIP");
        double center = machine.PositionX * DefaultWidth;
        Assert.True(center >= 140 - 1 && center <= DefaultWidth - 140 + 1, $"center {center} DIP");
    }

    [Fact]
    public void Update_GivenAWanderDecisionAgainstWalking_StaysIdle()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.75);

        Advance(machine, time, TimeSpan.FromSeconds(36.5));

        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0.5, machine.PositionX, 9);
    }

    [Fact]
    public void Update_GivenAutonomousWalking_DoesNotResetTheSleepCountdown()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine(random: 0.25);

        Advance(machine, time, TimeSpan.FromMinutes(4));
        Assert.NotEqual(MyJiuMiState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMinutes(1));
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);
    }

    [Fact]
    public void Update_GivenASleepDeadlineDuringAWalk_FinishesTheWalkThenSleeps()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) =
            CreateMachine(width: 40000, positionX: 0.05);
        machine.Summon(0.95);

        Advance(machine, time, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
        Assert.Equal(MyJiuMiState.Walk, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(51), TimeSpan.FromSeconds(1));
        Assert.Equal(0.95, machine.PositionX, 6);
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }

    [Fact]
    public void Update_GivenASleepDeadlineDuringADrag_WaitsForTheRelease()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        Advance(machine, time, TimeSpan.FromMinutes(4));
        machine.BeginDrag();
        Advance(machine, time, TimeSpan.FromSeconds(301));

        Assert.Equal(MyJiuMiState.Drag, machine.State);

        machine.EndDrag();

        Assert.Equal(MyJiuMiState.SleepIn, machine.State);
    }

    [Fact]
    public void Deactivate_GivenAnActiveCat_StopsUpdatesAndRestartsOnActivate()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateMachine();

        Advance(machine, time, TimeSpan.FromMinutes(1));
        machine.Deactivate();
        time.Advance(TimeSpan.FromMinutes(10));
        machine.Update();

        Assert.False(machine.IsActive);
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
        Assert.Equal(0, machine.FrameIndex);

        machine.Activate(DefaultWidth, 0.3);
        Assert.True(machine.IsActive);
        Assert.Equal(0.3, machine.PositionX, 9);

        Advance(machine, time, TimeSpan.FromMinutes(4));
        Assert.NotEqual(MyJiuMiState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMinutes(1));
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);
    }

    [Theory]
    [InlineData(-2, 0)]
    [InlineData(2, 1)]
    [InlineData(0.4, 0.4)]
    public void SetPosition_GivenAValue_ClampsItIntoTheRoom(double value, double expected)
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine();

        machine.SetPosition(value);

        Assert.Equal(expected, machine.PositionX, 9);
    }

    [Fact]
    public void Activate_GivenANonFinitePosition_UsesTheCenteredDefault()
    {
        (MyJiuMiStateMachine machine, _) = CreateMachine(positionX: double.NaN);

        Assert.Equal(MyJiuMiPositionRules.Default, machine.PositionX, 9);
    }

    private static (MyJiuMiStateMachine Machine, ManualTimeProvider Time) CreateMachine(
        double random = 0.25,
        double width = DefaultWidth,
        double positionX = 0.5)
    {
        var time = new ManualTimeProvider(_origin);
        var machine = new MyJiuMiStateMachine(time, new FixedRandom(random));
        machine.Activate(width, positionX);
        return (machine, time);
    }

    private static void Sleep(MyJiuMiStateMachine machine, ManualTimeProvider time)
    {
        Advance(machine, time, MyJiuMiInteractionRules.SleepDelay);
        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }
}
