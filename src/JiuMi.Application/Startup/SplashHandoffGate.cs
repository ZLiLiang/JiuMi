namespace JiuMi.Application.Startup;

/// <summary>
/// The startup handoff gate: the splash may leave only once the display floor has passed, the
/// animation has had its full length when it is longer, and initialization is ready.
/// </summary>
public sealed class SplashHandoffGate
{
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
    /// <returns>True when both gates are satisfied.</returns>
    public bool IsSatisfied(TimeSpan elapsed, bool isInitializationReady) =>
        isInitializationReady && elapsed >= _displayDuration;
}
