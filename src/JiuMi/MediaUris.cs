namespace JiuMi;

/// <summary>
/// The packaged media the interface plays.
/// </summary>
internal static class MediaUris
{
    /// <summary>The brand animation: played once by the splash, continuously by the home hero.</summary>
    public static Uri SplashAnimation { get; } = new("avares://JiuMi/Assets/splash.gif");

    /// <summary>The still that stands in for the animation when it is missing.</summary>
    public static Uri SplashPoster { get; } = new("avares://JiuMi/Assets/splash.png");
}
