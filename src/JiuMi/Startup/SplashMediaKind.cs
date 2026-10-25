namespace JiuMi.Startup;

/// <summary>
/// The rung of the resource ladder the splash landed on.
/// </summary>
internal enum SplashMediaKind
{
    /// <summary>The animation played once.</summary>
    Animation,

    /// <summary>The animation was unusable, so the still poster stands in for it.</summary>
    Poster,

    /// <summary>Neither asset survived; only the locked background and the ring are shown.</summary>
    BackgroundOnly,
}
