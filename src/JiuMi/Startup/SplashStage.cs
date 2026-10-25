namespace JiuMi.Startup;

/// <summary>
/// The startup stages the splash drives. A start that fails outright has its own terminal stage in
/// the architecture, which this slice does not reach.
/// </summary>
internal enum SplashStage
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
}
