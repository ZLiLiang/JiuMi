using JiuMi.Application.Startup;

namespace JiuMi.Application.Tests;

public sealed class SplashHandoffGateTests
{
    [Fact]
    public void IsSatisfied_GivenGifShorterThanTheFloor_ElapsedAtTheFloorAndReady_Satisfied()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        // The 1000ms floor binds here, not the 640ms animation.
        Assert.True(gate.IsSatisfied(TimeSpan.FromMilliseconds(1000), isInitializationReady: true));
    }

    [Fact]
    public void IsSatisfied_GivenGifLongerThanTheFloor_ElapsedAtTheFloor_NotSatisfied()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(3200));

        // Truncating the animation would stop it mid-frame, which reads as a stutter.
        Assert.False(gate.IsSatisfied(TimeSpan.FromMilliseconds(1000), isInitializationReady: true));
    }

    [Fact]
    public void IsSatisfied_GivenGifLongerThanTheFloor_ElapsedAtTheGifLength_Satisfied()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(3200));

        Assert.True(gate.IsSatisfied(TimeSpan.FromMilliseconds(3200), isInitializationReady: true));
    }

    [Fact]
    public void IsSatisfied_GivenInitializationNotReady_ElapsedBeyondBothGates_NotSatisfied()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        Assert.False(gate.IsSatisfied(TimeSpan.FromSeconds(30), isInitializationReady: false));
    }

    [Fact]
    public void RequiredDisplayDuration_GivenNoAnimation_IsTheFloor()
    {
        SplashHandoffGate gate = SplashHandoffGate.ForGif(TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMilliseconds(1000), gate.RequiredDisplayDuration);
    }
}
