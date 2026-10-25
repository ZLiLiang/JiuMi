using JiuMi.Application.Startup;

namespace JiuMi.Application.Tests;

public sealed class SplashHandoffGateTests
{
    [Fact]
    public void Evaluate_GivenGifShorterThanTheFloor_ElapsedAtTheFloorAndReady_HandsOff()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        // The 1000ms floor binds here, not the 640ms animation.
        Assert.Equal(
            SplashHandoffDecision.Handoff,
            gate.Evaluate(TimeSpan.FromMilliseconds(1000), isInitializationReady: true));
    }

    [Fact]
    public void Evaluate_GivenGifLongerThanTheFloor_ElapsedAtTheFloor_KeepsWaiting()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(3200));

        // Truncating the animation would stop it mid-frame, which reads as a stutter.
        Assert.Equal(
            SplashHandoffDecision.Wait,
            gate.Evaluate(TimeSpan.FromMilliseconds(1000), isInitializationReady: true));
    }

    [Fact]
    public void Evaluate_GivenGifLongerThanTheFloor_ElapsedAtTheGifLength_HandsOff()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(3200));

        Assert.Equal(
            SplashHandoffDecision.Handoff,
            gate.Evaluate(TimeSpan.FromMilliseconds(3200), isInitializationReady: true));
    }

    [Fact]
    public void Evaluate_GivenReady_ElapsedBeforeTheFloor_KeepsWaiting()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        Assert.Equal(
            SplashHandoffDecision.Wait,
            gate.Evaluate(TimeSpan.FromMilliseconds(999), isInitializationReady: true));
    }

    [Fact]
    public void Evaluate_GivenNotReady_ElapsedJustBeforeTheTimeout_KeepsWaiting()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        Assert.Equal(
            SplashHandoffDecision.Wait,
            gate.Evaluate(TimeSpan.FromMilliseconds(9999), isInitializationReady: false));
    }

    [Fact]
    public void Evaluate_GivenNotReady_ElapsedAtTheTimeout_ForcesHandoff()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        // The fallback keeps a slow initialization from holding the window indefinitely.
        Assert.Equal(
            SplashHandoffDecision.ForcedHandoff,
            gate.Evaluate(TimeSpan.FromMilliseconds(10000), isInitializationReady: false));
    }

    [Fact]
    public void Evaluate_GivenReady_ElapsedAtTheTimeout_HandsOffNormally()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        Assert.Equal(
            SplashHandoffDecision.Handoff,
            gate.Evaluate(TimeSpan.FromMilliseconds(10000), isInitializationReady: true));
    }

    [Fact]
    public void Evaluate_GivenNotReady_ElapsedBeyondTheTimeout_ForcesHandoff()
    {
        var gate = SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640));

        Assert.Equal(
            SplashHandoffDecision.ForcedHandoff,
            gate.Evaluate(TimeSpan.FromSeconds(30), isInitializationReady: false));
    }

    [Fact]
    public void RequiredDisplayDuration_GivenNoAnimation_IsTheFloor()
    {
        SplashHandoffGate gate = SplashHandoffGate.ForGif(TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMilliseconds(1000), gate.RequiredDisplayDuration);
    }
}
