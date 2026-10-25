namespace JiuMi.Application.Startup;

/// <summary>
/// The handoff gate's verdict for one instant of the splash.
/// </summary>
public enum SplashHandoffDecision
{
    /// <summary>The conditions for leaving the splash are not met yet.</summary>
    Wait,

    /// <summary>Initialization is ready and the display gate has passed.</summary>
    Handoff,

    /// <summary>The readiness timeout has passed, so the splash must leave even though initialization is not ready.</summary>
    ForcedHandoff,
}
