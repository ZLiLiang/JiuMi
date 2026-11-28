using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

/// <summary>
/// The accepted micro-motion values: breathing and sway at rest, and the standing jump phases.
/// </summary>
public sealed class MyJiuMiMotionRulesTests
{
    [Fact]
    public void Sample_AtTheStartOfAVisit_OnlyOffsetsTheInitialSway()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(TimeSpan.Zero, MyJiuMiState.LyingIdle);

        Assert.Equal(1, motion.ScaleX, 9);
        Assert.Equal(1, motion.ScaleY, 9);
        Assert.Equal(Math.Sin(MyJiuMiMotionRules.SwayPhaseRadians) * 0.6, motion.RotationDegrees, 9);
        Assert.Equal(0, motion.OffsetY, 9);
        Assert.Equal(1, motion.ShadowScale, 9);
        Assert.Equal(1, motion.ShadowOpacity, 9);
    }

    [Fact]
    public void Sample_GivenAQuarterBreath_RaisesTheBodyAndNarrowsIt()
    {
        TimeSpan elapsed = TimeSpan.FromTicks(MyJiuMiMotionRules.BreathPeriod.Ticks / 4);

        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(elapsed, MyJiuMiState.LyingIdle);

        Assert.Equal(1 + 0.015, motion.ScaleY, 9);
        Assert.Equal(1 - (0.015 * 0.35), motion.ScaleX, 9);
        Assert.Equal(1 - 0.08, motion.ShadowScale, 9);
        Assert.Equal(1, motion.ShadowOpacity, 9);
    }

    [Fact]
    public void Sample_GivenSleep_UsesTheSlowerAndShallowerBreath()
    {
        TimeSpan elapsed = TimeSpan.FromTicks(
            (long)(MyJiuMiMotionRules.BreathPeriod.Ticks * 1.5 / 4));

        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(elapsed, MyJiuMiState.Sleep);

        Assert.Equal(1 + (0.015 * 0.8), motion.ScaleY, 6);
        Assert.Equal(1 - (0.015 * 0.8 * 0.35), motion.ScaleX, 6);
        Assert.Equal(1 - 0.08, motion.ShadowScale, 6);
    }

    [Fact]
    public void Sample_GivenTheSwayPeak_ReachesTheAcceptedAmplitude()
    {
        double peakSeconds =
            ((Math.PI / 2) - MyJiuMiMotionRules.SwayPhaseRadians) *
            MyJiuMiMotionRules.BreathPeriod.TotalSeconds *
            MyJiuMiMotionRules.SwayPeriodScale /
            (Math.PI * 2);

        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(
            TimeSpan.FromSeconds(peakSeconds),
            MyJiuMiState.LyingIdle);

        Assert.Equal(0.6, motion.RotationDegrees, 6);
    }

    [Fact]
    public void Sample_GivenWalking_DoesNotSway()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(
            TimeSpan.FromSeconds(1),
            MyJiuMiState.Walk);

        Assert.Equal(0, motion.RotationDegrees, 9);
    }

    [Fact]
    public void SampleJump_AtTheStart_IsTheRestPose()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.SampleJump(TimeSpan.Zero);

        Assert.Equal(MyJiuMiMotion.None, motion);
    }

    [Fact]
    public void SampleJump_GivenTheSquash_ShrinksHeightAndWidensTheBody()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.SampleJump(
            TimeSpan.FromTicks(MyJiuMiMotionRules.JumpDuration.Ticks * 7 / 100));

        Assert.Equal(0.935, motion.ScaleY, 9);
        Assert.Equal(1.04, motion.ScaleX, 9);
        Assert.Equal(0, motion.OffsetY, 9);
        Assert.Equal(1, motion.ShadowScale, 9);
    }

    [Fact]
    public void SampleJump_GivenTheRise_LiftsTheCatAndShrinksTheShadow()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.SampleJump(
            TimeSpan.FromTicks(MyJiuMiMotionRules.JumpDuration.Ticks * 29 / 100));

        Assert.Equal(-32.9, motion.OffsetY, 1);
        Assert.Equal(1.04, motion.ScaleY, 9);
        Assert.Equal(0.97, motion.ScaleX, 9);
        Assert.Equal(0.6675, motion.ShadowScale, 3);
        Assert.Equal(0.6675, motion.ShadowOpacity, 3);
    }

    [Fact]
    public void SampleJump_GivenTheApex_UsesTheAcceptedJumpHeight()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.SampleJump(
            TimeSpan.FromTicks(MyJiuMiMotionRules.JumpDuration.Ticks * 43 / 100));

        Assert.Equal(-MyJiuMiMotionRules.JumpHeightDip, motion.OffsetY, 2);
        Assert.Equal(0.62, motion.ShadowScale, 3);
        Assert.Equal(0.62, motion.ShadowOpacity, 3);
        Assert.True(motion.ShadowOpacity >= 0.45);
    }

    [Fact]
    public void SampleJump_GivenTheLandingSquash_ShrinksHeightAndWidensTheBody()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.SampleJump(
            TimeSpan.FromTicks(MyJiuMiMotionRules.JumpDuration.Ticks * 77 / 100));

        Assert.Equal(0, motion.OffsetY, 9);
        Assert.Equal(0.90, motion.ScaleY, 9);
        Assert.Equal(1.07, motion.ScaleX, 9);
        Assert.Equal(1.08, motion.ShadowScale, 9);
    }

    [Fact]
    public void SampleJump_AtTheEnd_ReturnsToTheRestPose()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.SampleJump(MyJiuMiMotionRules.JumpDuration);

        Assert.Equal(MyJiuMiMotion.None, motion);
    }

    [Fact]
    public void JumpHeightDip_IsTheAcceptedShareOfTheCatSlot()
    {
        Assert.Equal(37.6, MyJiuMiMotionRules.JumpHeightDip, 9);
    }

    [Theory]
    [InlineData(MyJiuMiSitWatchDirection.Right, -200, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Right, 0, 0.0)]
    [InlineData(MyJiuMiSitWatchDirection.Right, 200, 3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Right, -500, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Right, 500, 3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Left, -200, 3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Left, 200, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.UpLeft, -200, 3.0)]
    [InlineData(MyJiuMiSitWatchDirection.DownLeft, 200, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.UpRight, -200, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.DownRight, 200, 3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Up, -200, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Down, 200, 3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Center, -200, -3.0)]
    [InlineData(MyJiuMiSitWatchDirection.Center, 200, 3.0)]
    public void ComputeSitWatchTiltDegrees_GivenCursorOffsetY_ClampsToThreeDegrees(
        MyJiuMiSitWatchDirection direction,
        double cursorOffsetY,
        double expectedDegrees)
    {
        double tilt = MyJiuMiMotionRules.ComputeSitWatchTiltDegrees(direction, cursorOffsetY);

        Assert.Equal(expectedDegrees, tilt, 6);
    }

    [Fact]
    public void Sample_GivenSitWatchAndTilt_CombinesBreathingAndClampedMicroTilt()
    {
        TimeSpan elapsed = TimeSpan.FromTicks(MyJiuMiMotionRules.BreathPeriod.Ticks / 4);

        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(elapsed, MyJiuMiState.SitWatch, sitWatchTiltDegrees: 2.5);

        Assert.Equal(1 + 0.015, motion.ScaleY, 9);
        Assert.Equal(1 - (0.015 * 0.35), motion.ScaleX, 9);
        Assert.Equal(2.5, motion.RotationDegrees, 9);
    }

    [Fact]
    public void Sample_GivenSitWatchWithoutCursorTilt_DoesNotSway()
    {
        MyJiuMiMotion motion = MyJiuMiMotionRules.Sample(TimeSpan.FromSeconds(1), MyJiuMiState.SitWatch);

        Assert.Equal(0, motion.RotationDegrees, 9);
    }

    [Fact]
    public void SmoothSitWatchTiltDegrees_GivenPositiveStep_MovesSmoothlyTowardTargetAndClamps()
    {
        double halfway = MyJiuMiMotionRules.SmoothSitWatchTiltDegrees(
            currentDegrees: 0,
            targetDegrees: 5,
            elapsed: TimeSpan.FromMilliseconds(80));

        Assert.True(halfway > 1.5 && halfway < 3.0, $"expected smooth step in (1.5, 3.0), got {halfway}");

        double snappedZero = MyJiuMiMotionRules.SmoothSitWatchTiltDegrees(
            currentDegrees: 0.005,
            targetDegrees: 0,
            elapsed: TimeSpan.FromMilliseconds(16));

        Assert.Equal(0, snappedZero, 9);
    }
}
