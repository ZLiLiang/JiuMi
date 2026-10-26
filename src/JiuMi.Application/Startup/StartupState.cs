namespace JiuMi.Application.Startup;

/// <summary>
/// The stages the startup sequence moves through.
/// </summary>
public enum StartupState
{
    /// <summary>Nothing has been put on screen yet.</summary>
    Bootstrapping,

    /// <summary>The splash is up, but no frame of it has been drawn.</summary>
    SplashShown,

    /// <summary>The animation is playing.</summary>
    Animating,

    /// <summary>The drawing has stopped and the shell is not ready yet.</summary>
    Waiting,

    /// <summary>Both windows are fading across.</summary>
    Handoff,

    /// <summary>The shell is up and the splash is gone.</summary>
    Running,

    /// <summary>Startup failed; the error window decides whether the process can continue.</summary>
    Faulted,
}
