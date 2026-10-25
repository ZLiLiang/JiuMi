using JiuMi.Application.Startup;

namespace JiuMi.Application.Tests;

public sealed class StartupSessionTests
{
    [Fact]
    public void MarkReady_GivenFinishedBeforeTheTimeout_LeavesTheSessionTimedOutFalse()
    {
        StartupSession session = CreateSession();
        Assert.Equal(SplashHandoffDecision.Wait, session.Evaluate(TimeSpan.FromSeconds(2)));

        session.MarkReady();

        Assert.True(session.IsReady);
        Assert.False(session.HasTimedOut);
    }

    [Fact]
    public void MarkReady_GivenForcedHandoff_LeavesTheSessionTimedOut()
    {
        StartupSession session = CreateSession();
        Assert.Equal(
            SplashHandoffDecision.ForcedHandoff,
            session.Evaluate(TimeSpan.FromSeconds(10)));

        session.MarkReady();

        Assert.True(session.HasTimedOut);
        Assert.True(session.IsReady);
    }

    [Fact]
    public void MarkFaulted_GivenAFailedStep_BlocksReadiness()
    {
        StartupSession session = CreateSession();
        session.Evaluate(TimeSpan.FromSeconds(2));

        session.MarkFaulted();
        session.MarkReady();

        Assert.True(session.HasFaulted);
        Assert.False(session.IsReady);
        Assert.Equal(StartupState.Faulted, session.State);
    }

    [Fact]
    public void Constructor_GivenANewSession_StartsInBootstrapping()
    {
        StartupSession session = CreateSession();

        Assert.Equal(StartupState.Bootstrapping, session.State);
    }

    [Fact]
    public void TransitionTo_GivenEveryRenderedState_RaisesOneEventPerChange()
    {
        StartupSession session = CreateSession();
        var raised = new List<StartupState>();
        session.StateChanged += (_, state) => raised.Add(state);

        StartupState[] transitions =
        [
            StartupState.SplashShown,
            StartupState.Animating,
            StartupState.Waiting,
            StartupState.Handoff,
            StartupState.Running,
            StartupState.Faulted,
        ];

        foreach (StartupState state in transitions)
        {
            session.TransitionTo(state);
        }

        Assert.Equal(transitions, raised);
        Assert.Equal(StartupState.Faulted, session.State);
    }

    [Fact]
    public void TransitionTo_GivenTheCurrentState_DoesNotRaiseAgain()
    {
        StartupSession session = CreateSession();
        int raised = 0;
        session.StateChanged += (_, _) => raised++;

        session.TransitionTo(StartupState.Bootstrapping);
        session.TransitionTo(StartupState.SplashShown);
        session.TransitionTo(StartupState.SplashShown);

        Assert.Equal(1, raised);
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
        session.MarkReady();

        Assert.Equal(SplashHandoffDecision.Wait, session.Evaluate(TimeSpan.FromMilliseconds(999)));
        Assert.Equal(SplashHandoffDecision.Handoff, session.Evaluate(TimeSpan.FromMilliseconds(1000)));
    }

    private static StartupSession CreateSession() =>
        new(SplashHandoffGate.ForGif(TimeSpan.FromMilliseconds(640)));
}
