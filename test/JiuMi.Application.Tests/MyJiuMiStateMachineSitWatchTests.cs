using System.Collections.ObjectModel;
using JiuMi.Application.MyJiuMi;
using static JiuMi.Application.Tests.MyJiuMiTestTimeline;

namespace JiuMi.Application.Tests;

/// <summary>
/// Behaviour of the sit-watch head-tracking and tired-sleep loop in <see cref="MyJiuMiStateMachine"/>:
/// entry conditions, three-way gaze with hysteresis and debounce, effective movement, patience exhaustion
/// splits for page and desktop modes, sleep non-wakeup, and legacy pack fallback.
/// </summary>
public sealed class MyJiuMiStateMachineSitWatchTests
{
    private const double DefaultWidth = 800;

    private static readonly DateTimeOffset _origin =
        new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activate_GivenSitWatchManifest_StartsInSitWatchFacingCenter()
    {
        (MyJiuMiStateMachine machine, _) = CreateSitWatchMachine();

        Assert.True(machine.SupportsSitWatch);
        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);
        Assert.False(machine.IsFacingRight);
        Assert.True(machine.IsAwake);
    }

    [Fact]
    public void Activate_GivenLegacyManifestWithoutSitWatch_FallsBackToLyingIdle()
    {
        var time = new ManualTimeProvider(_origin);
        MyJiuMiPetPackManifest legacy = MyJiuMiAnimationCatalog.CreateFallbackManifest();
        var machine = new MyJiuMiStateMachine(time, new FixedRandom(0.25), legacy);

        machine.Activate(DefaultWidth, 0.5);

        Assert.False(machine.SupportsSitWatch);
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);
    }

    [Fact]
    public void Wake_GivenSitWatchManifest_EntersSitWatchWhenWakeCompletes()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine();
        SleepFromSitWatch(machine, time);

        MyJiuMiClickEffect effect = machine.Click();
        Assert.Equal(MyJiuMiClickEffect.Wake, effect);
        Assert.Equal(MyJiuMiState.Wake, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));

        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
        Assert.False(machine.IsFacingRight);
    }

    [Fact]
    public void Summon_GivenSitWatchManifest_ReturnsToSitWatchWhenWalkFinishes()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine();

        machine.Summon(0.8);
        Advance(machine, time, TimeSpan.FromMilliseconds(480));
        Assert.Equal(MyJiuMiState.Walk, machine.State);
        Assert.True(machine.IsFacingRight);

        Advance(machine, time, TimeSpan.FromSeconds(4));

        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
        Assert.False(machine.IsFacingRight);
        Assert.Equal(0.8, machine.PositionX, 6);
    }

    [Theory]
    [InlineData(-61, MyJiuMiSitWatchDirection.Left)]
    [InlineData(-60, MyJiuMiSitWatchDirection.Center)]
    [InlineData(0, MyJiuMiSitWatchDirection.Center)]
    [InlineData(60, MyJiuMiSitWatchDirection.Center)]
    [InlineData(61, MyJiuMiSitWatchDirection.Right)]
    public void UpdateCursor_GivenFirstSampleInSitWatch_UsesSixtyDipThreshold(
        double offsetX,
        MyJiuMiSitWatchDirection expected)
    {
        (MyJiuMiStateMachine machine, _) = CreateSitWatchMachine();

        machine.UpdateCursor(offsetX, 0);

        Assert.Equal(expected, machine.SitWatchDirection);
    }

    [Fact]
    public void UpdateCursor_GivenHoverNearThreshold_AppliesFifteenDipHysteresis()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine();

        machine.UpdateCursor(0, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);

        // Crossing -60 but staying inside -60 - 15 (-75) holds Center.
        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(-70, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);

        // Crossing beyond -75 switches to Left.
        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(-76, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        // Moving back across -60 but staying left of -60 + 15 (-45) holds Left.
        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(-50, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        // Crossing -45 returns to Center.
        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(-45, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);

        // Symmetric check on the right boundary (+60 ± 15).
        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(75, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);

        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(76, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Right, machine.SitWatchDirection);

        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(46, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Right, machine.SitWatchDirection);

        time.Advance(TimeSpan.FromMilliseconds(150));
        machine.UpdateCursor(45, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);
    }

    [Fact]
    public void UpdateCursor_GivenRapidDirectionChanges_EnforcesHundredMillisecondMinimumSwitchInterval()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine();

        machine.UpdateCursor(-100, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        // 50 ms later the cursor jumps right, but debounce holds Left until 100 ms has elapsed.
        time.Advance(TimeSpan.FromMilliseconds(50));
        machine.UpdateCursor(100, 0);
        machine.Update();
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        // Once 100 ms passes, Update completes the deferred switch without needing another move.
        time.Advance(TimeSpan.FromMilliseconds(50));
        machine.Update();
        Assert.Equal(MyJiuMiSitWatchDirection.Right, machine.SitWatchDirection);
    }

    [Fact]
    public void Update_GivenSubEightDipCursorJitter_DoesNotResetFourSecondStillnessTimer()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);
        machine.UpdateCursor(0, 0);

        for (int second = 0; second < 4; second++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            machine.UpdateCursor(second % 2 == 0 ? 5 : -3, 0);
            machine.Update();
        }

        Assert.Equal(MyJiuMiState.SitToLie, machine.State);
    }

    [Fact]
    public void Update_GivenOverEightDipCursorMove_ResetsStillnessTimerUntilTwelveSecondCap()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);
        machine.UpdateCursor(0, 0);

        // Moving 20 DIP every 3 seconds keeps the stillness timer alive up to 11 seconds.
        for (int step = 1; step <= 3; step++)
        {
            Advance(machine, time, TimeSpan.FromSeconds(3));
            Assert.Equal(MyJiuMiState.SitWatch, machine.State);
            machine.UpdateCursor(step % 2 == 0 ? 0 : 120, 0);
        }

        Advance(machine, time, TimeSpan.FromSeconds(2.5));
        Assert.Equal(MyJiuMiState.SitWatch, machine.State);

        // At 12 seconds total, continuous watch patience runs out even with recent cursor motion.
        Advance(machine, time, TimeSpan.FromSeconds(0.5));
        Assert.Equal(MyJiuMiState.SitToLie, machine.State);
    }

    [Fact]
    public void Update_GivenDesktopModePatienceExhausted_AlwaysPlaysSitToLieLyingBufferThenSleeps()
    {
        // Even with random >= 0.75 (which would wander in page mode), desktop mode sleeps 100%.
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.9);
        machine.SetWanderingEnabled(false);

        Advance(machine, time, TimeSpan.FromSeconds(4));
        Assert.Equal(MyJiuMiState.SitToLie, machine.State);

        // sit-to-lie is 500 ms (4 x 125 ms) -> LyingIdle buffer for 4 s -> SleepIn -> Sleep.
        Advance(machine, time, TimeSpan.FromMilliseconds(500));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(3.5));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(0.5));
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }

    [Fact]
    public void Update_GivenMissingSitToLieSlot_SwitchesDirectlyToLyingIdleBufferThenSleeps()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(
            random: 0.25,
            includeSitToLie: false);

        Advance(machine, time, TimeSpan.FromSeconds(4));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(4));
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);
    }

    [Fact]
    public void Update_GivenPageModeSeventyFivePercentRoll_LiesDownAndSleeps()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.74);

        Advance(machine, time, TimeSpan.FromSeconds(4));

        Assert.Equal(MyJiuMiState.SitToLie, machine.State);
    }

    [Fact]
    public void Update_GivenPageModeTwentyFivePercentRoll_WandersAndReturnsToSitWatch()
    {
        // 0.75 hits the 25% wander branch; subsequent rolls pick a valid wander target.
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.75);

        Advance(machine, time, TimeSpan.FromSeconds(4));
        Assert.Equal(MyJiuMiState.Stretch, machine.State);

        Advance(machine, time, TimeSpan.FromMilliseconds(480));
        Assert.Equal(MyJiuMiState.Walk, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(6));
        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
        Assert.NotEqual(0.5, machine.PositionX);
    }

    [Fact]
    public void UpdateCursor_GivenSleepInOrSleep_DoesNotWakeTheCat()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);

        // Reach SleepIn.
        Advance(machine, time, TimeSpan.FromSeconds(4));
        Advance(machine, time, TimeSpan.FromMilliseconds(500));
        Advance(machine, time, TimeSpan.FromSeconds(4));
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);

        machine.UpdateCursor(200, -100);
        machine.Update();
        Assert.Equal(MyJiuMiState.SleepIn, machine.State);

        // Reach Sleep.
        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);

        for (int step = 0; step < 10; step++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            machine.UpdateCursor(step % 2 == 0 ? -300 : 300, -150);
            machine.Update();
            Assert.Equal(MyJiuMiState.Sleep, machine.State);
            Assert.False(machine.IsAwake);
        }
    }

    [Fact]
    public void Click_GivenFourSecondLyingBuffer_CancelsImminentSleepAndReturnsToSitWatch()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);
        Advance(machine, time, TimeSpan.FromSeconds(4));
        Advance(machine, time, TimeSpan.FromMilliseconds(500));
        Assert.Equal(MyJiuMiState.LyingIdle, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(2));
        MyJiuMiClickEffect effect = machine.Click();
        Assert.Equal(MyJiuMiClickEffect.React, effect);

        Advance(machine, time, TimeSpan.FromMilliseconds(240));
        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
    }

    [Fact]
    public void Update_GivenCumulativeSlowMovesExceedingEightDip_ResetsFourSecondStillnessTimer()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);
        machine.UpdateCursor(0, 0);

        // Three 3 DIP steps accumulate to 9 DIP (> 8 DIP) at t = 3 s, resetting stillness until t = 7 s.
        time.Advance(TimeSpan.FromSeconds(1));
        machine.UpdateCursor(3, 0);
        machine.Update();

        time.Advance(TimeSpan.FromSeconds(1));
        machine.UpdateCursor(6, 0);
        machine.Update();

        time.Advance(TimeSpan.FromSeconds(1));
        machine.UpdateCursor(9, 0);
        machine.Update();

        Advance(machine, time, TimeSpan.FromSeconds(3.5));
        Assert.Equal(MyJiuMiState.SitWatch, machine.State);

        Advance(machine, time, TimeSpan.FromSeconds(0.5));
        Assert.Equal(MyJiuMiState.SitToLie, machine.State);
    }

    [Fact]
    public void Update_GivenNarrowRoomOnPageWanderRoll_FallsBackToSitToLieSleep()
    {
        var time = new ManualTimeProvider(_origin);
        MyJiuMiPetPackManifest manifest = CreateSitWatchManifest(includeSitToLie: true);
        var machine = new MyJiuMiStateMachine(time, new FixedRandom(0.9), manifest);
        machine.Activate(stageWidth: 200, positionX: 0.5);

        Advance(machine, time, TimeSpan.FromSeconds(4));

        Assert.Equal(MyJiuMiState.SitToLie, machine.State);
    }

    [Fact]
    public void EndDrag_GivenSitWatchManifest_ReturnsToSitWatchWhenAwake()
    {
        (MyJiuMiStateMachine machine, _) = CreateSitWatchMachine();
        machine.BeginDrag();
        Assert.Equal(MyJiuMiState.Drag, machine.State);

        machine.EndDrag();

        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
    }

    [Fact]
    public void UpdateCursor_GivenNonFiniteCoordinates_IsIgnored()
    {
        (MyJiuMiStateMachine machine, _) = CreateSitWatchMachine();
        machine.UpdateCursor(-100, -50);
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        machine.UpdateCursor(double.NaN, double.PositiveInfinity);

        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);
        Assert.Equal(-50, machine.CursorOffsetY, 9);
    }

    [Fact]
    public void Wake_GivenPreSleepCursorOffset_ClearsStaleDirectionAndStartsCenterUntilFreshMove()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);
        machine.UpdateCursor(-150, -100);
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        SleepFromSitWatch(machine, time);

        Assert.Equal(MyJiuMiClickEffect.Wake, machine.Click());
        Advance(machine, time, TimeSpan.FromMilliseconds(450));

        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);
        Assert.False(machine.HasCursorPosition);
    }

    [Fact]
    public void Summon_GivenPreWalkCursorOffset_ClearsStaleDirectionUponArrival()
    {
        (MyJiuMiStateMachine machine, ManualTimeProvider time) = CreateSitWatchMachine(random: 0.25);
        machine.UpdateCursor(-150, 0);
        Assert.Equal(MyJiuMiSitWatchDirection.Left, machine.SitWatchDirection);

        machine.Summon(0.8);
        Advance(machine, time, TimeSpan.FromSeconds(5));

        Assert.Equal(MyJiuMiState.SitWatch, machine.State);
        Assert.Equal(MyJiuMiSitWatchDirection.Center, machine.SitWatchDirection);
        Assert.False(machine.HasCursorPosition);
    }

    private static void SleepFromSitWatch(MyJiuMiStateMachine machine, ManualTimeProvider time)
    {
        Advance(machine, time, TimeSpan.FromSeconds(4));
        Advance(machine, time, TimeSpan.FromMilliseconds(500));
        Advance(machine, time, TimeSpan.FromSeconds(4));
        Advance(machine, time, TimeSpan.FromMilliseconds(450));
        Assert.Equal(MyJiuMiState.Sleep, machine.State);
    }

    private static (MyJiuMiStateMachine Machine, ManualTimeProvider Time) CreateSitWatchMachine(
        double random = 0.25,
        bool includeSitToLie = true)
    {
        var time = new ManualTimeProvider(_origin);
        MyJiuMiPetPackManifest manifest = CreateSitWatchManifest(includeSitToLie);
        var machine = new MyJiuMiStateMachine(time, new FixedRandom(random), manifest);
        machine.Activate(DefaultWidth, 0.5);
        return (machine, time);
    }

    private static MyJiuMiPetPackManifest CreateSitWatchManifest(bool includeSitToLie)
    {
        MyJiuMiPetPackManifest baseManifest = MyJiuMiAnimationCatalog.CreateFallbackManifest();
        MyJiuMiPetPackAnchor anchor = baseManifest.DefaultAnchor;
        var interactions = new Dictionary<string, MyJiuMiPetPackClip>(StringComparer.Ordinal)
        {
            [MyJiuMiPetPackManifest.SitWatchLeftInteractionKey] = new(
                MyJiuMiPetPackManifest.SitWatchLeftInteractionKey,
                [new MyJiuMiPetPackFrame("sit-watch-left/00.png", TimeSpan.FromMilliseconds(500), anchor)],
                Loops: true,
                anchor),
            [MyJiuMiPetPackManifest.SitWatchCenterInteractionKey] = new(
                MyJiuMiPetPackManifest.SitWatchCenterInteractionKey,
                [new MyJiuMiPetPackFrame("sit-watch-center/00.png", TimeSpan.FromMilliseconds(500), anchor)],
                Loops: true,
                anchor),
            [MyJiuMiPetPackManifest.SitWatchRightInteractionKey] = new(
                MyJiuMiPetPackManifest.SitWatchRightInteractionKey,
                [new MyJiuMiPetPackFrame("sit-watch-right/00.png", TimeSpan.FromMilliseconds(500), anchor)],
                Loops: true,
                anchor),
        };

        if (includeSitToLie)
        {
            interactions[MyJiuMiPetPackManifest.SitToLieInteractionKey] = new(
                MyJiuMiPetPackManifest.SitToLieInteractionKey,
                [
                    new MyJiuMiPetPackFrame("sit-to-lie/00.png", TimeSpan.FromMilliseconds(125), anchor),
                    new MyJiuMiPetPackFrame("sit-to-lie/01.png", TimeSpan.FromMilliseconds(125), anchor),
                    new MyJiuMiPetPackFrame("sit-to-lie/02.png", TimeSpan.FromMilliseconds(125), anchor),
                    new MyJiuMiPetPackFrame("sit-to-lie/03.png", TimeSpan.FromMilliseconds(125), anchor),
                ],
                Loops: false,
                anchor);
        }

        return baseManifest with
        {
            Interactions = new ReadOnlyDictionary<string, MyJiuMiPetPackClip>(interactions),
        };
    }
}
