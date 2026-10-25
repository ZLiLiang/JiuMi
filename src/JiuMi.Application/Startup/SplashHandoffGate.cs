namespace JiuMi.Application.Startup;

/// <summary>
/// The startup handoff gate: the splash may leave only once the display floor has passed, the
/// animation has had its full length when it is longer, and initialization is ready. Readiness has
/// a timeout so a slow initialization cannot hold the window indefinitely.
/// </summary>
public sealed class SplashHandoffGate
{
    /// <summary>
    /// Gets how long initialization may take before the splash leaves without it.
    /// </summary>
    public static TimeSpan ReadinessTimeout => TimeSpan.FromMilliseconds(10000);

    private static readonly TimeSpan _minimumDisplayDuration = TimeSpan.FromMilliseconds(1000);

    private readonly TimeSpan _displayDuration;

    private SplashHandoffGate(TimeSpan displayDuration) => _displayDuration = displayDuration;

    /// <summary>
    /// Creates the gate for a splash that plays an animation of the given length. The two time gates
    /// are never added up: the later one wins.
    /// </summary>
    /// <param name="gifDuration">The full length of the single animation pass, or zero without one.</param>
    /// <returns>The gate to evaluate against elapsed time.</returns>
    public static SplashHandoffGate ForGif(TimeSpan gifDuration) =>
        new(gifDuration > _minimumDisplayDuration ? gifDuration : _minimumDisplayDuration);

    /// <summary>
    /// Gets the time the splash must stay up: the display floor and the animation length, whichever
    /// is later. They never add up.
    /// </summary>
    public TimeSpan RequiredDisplayDuration => _displayDuration;

    /// <summary>
    /// Decides whether the splash can hand over to the main window.
    /// </summary>
    /// <param name="elapsed">Time measured since the first rendered splash frame.</param>
    /// <param name="isInitializationReady">Whether every startup step has completed.</param>
    /// <returns>
    /// <see cref="SplashHandoffDecision.Handoff"/> when the display gate has passed and initialization
    /// is ready, <see cref="SplashHandoffDecision.ForcedHandoff"/> when the readiness timeout has passed
    /// first, and <see cref="SplashHandoffDecision.Wait"/> otherwise.
    /// </returns>
    public SplashHandoffDecision Evaluate(TimeSpan elapsed, bool isInitializationReady)
    {
        if (elapsed < _displayDuration)
        {
            return SplashHandoffDecision.Wait;
        }

        if (isInitializationReady)
        {
            return SplashHandoffDecision.Handoff;
        }

        return elapsed >= ReadinessTimeout
            ? SplashHandoffDecision.ForcedHandoff
            : SplashHandoffDecision.Wait;
    }
}
