namespace JiuMi.Application.Startup;

/// <summary>
/// Tracks how the startup work is doing: whether it is ready, whether it finished after the
/// readiness timeout, and whether it failed outright.
/// </summary>
public sealed class StartupSession
{
    private readonly SplashHandoffGate _gate;

    private bool _isReady;
    private bool _hasFaulted;
    private bool _hasTimedOut;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupSession"/> class.
    /// </summary>
    /// <param name="gate">The handoff gate the session evaluates against.</param>
    public StartupSession(SplashHandoffGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        _gate = gate;
    }

    /// <summary>
    /// Gets a value indicating whether every startup step has completed.
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// Gets a value indicating whether a startup step failed outright, which is terminal.
    /// </summary>
    public bool HasFaulted => _hasFaulted;

    /// <summary>
    /// Gets a value indicating whether the splash had to leave before the work was ready.
    /// </summary>
    public bool HasTimedOut => _hasTimedOut;

    /// <summary>
    /// Evaluates the handoff gate and remembers a forced handoff.
    /// </summary>
    /// <param name="elapsed">Time measured since the first rendered splash frame.</param>
    /// <returns>The gate's verdict.</returns>
    public SplashHandoffDecision Evaluate(TimeSpan elapsed)
    {
        SplashHandoffDecision decision = _gate.Evaluate(elapsed, _isReady);
        if (decision == SplashHandoffDecision.ForcedHandoff)
        {
            _hasTimedOut = true;
        }

        return decision;
    }

    /// <summary>
    /// Marks the startup work complete. Read <see cref="HasTimedOut"/> before calling to learn whether
    /// the splash had already left.
    /// </summary>
    public void MarkReady()
    {
        if (_hasFaulted)
        {
            return;
        }

        _isReady = true;
    }

    /// <summary>
    /// Marks the startup work as failed; the state is terminal.
    /// </summary>
    public void MarkFaulted() => _hasFaulted = true;
}
