using JiuMi.Application.Startup;

namespace JiuMi.Application.Tests;

public sealed class StartupSessionTests
{
    [Fact]
    public void MarkReady_GivenFinishedBeforeTheTimeout_IsNotRetroactive()
    {
        StartupSession session = CreateSession();
        Assert.Equal(SplashHandoffDecision.Wait, session.Evaluate(TimeSpan.FromSeconds(2)));

        bool completedLate = session.MarkReady();

        Assert.False(completedLate);
        Assert.True(session.IsReady);
        Assert.False(session.HasTimedOut);
    }

    [Fact]
    public void MarkReady_GivenForcedHandoff_ReportsALateCompletion()
    {
        StartupSession session = CreateSession();
        Assert.Equal(
            SplashHandoffDecision.ForcedHandoff,
            session.Evaluate(TimeSpan.FromSeconds(10)));

        bool completedLate = session.MarkReady();

        Assert.True(completedLate);
        Assert.True(session.HasTimedOut);
        Assert.True(session.IsReady);
    }

    [Fact]
    public void MarkFaulted_GivenAFailedStep_LeavesAReadySessionUntouched()
    {
        StartupSession session = CreateSession();
        session.Evaluate(TimeSpan.FromSeconds(2));

        session.MarkFaulted();

        Assert.True(session.HasFaulted);
        Assert.False(session.MarkReady());
        Assert.False(session.IsReady);
    }

    [Fact]
    public void Evaluate_GivenAForcedHandoff_RemembersTheTimeout()
    {
        StartupSession session = CreateSession();

        Assert.Equal(SplashHandoffDecision.Wait, session.Evaluate(TimeSpan.FromSeconds(9)));
        Assert.False(session.HasTimedOut);

        Assert.Equal(SplashHandoffDecision.ForcedHandoff, session.Evaluate(TimeSpan.FromSeconds(10)));
        Assert.True(session.HasTimedOut);
    }

    [Fact]
    public void Evaluate_GivenAReadySession_HandsOffAfterTheDisplayGate()
    {
        StartupSession session = CreateSession();
        Assert.False(session.MarkReady());

        Assert.Equal(SplashHandoffDecision.Wait, session.Evaluate(TimeSpan.FromMilliseconds(999)));
        Assert.Equal(SplashHandoffDecision.Handoff, session.Evaluate(TimeSpan.FromMilliseconds(1000)));
    }

    private static StartupSession CreateSession() =>
        new(SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640)));
}
