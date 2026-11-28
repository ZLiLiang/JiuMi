namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The timing, movement and overlay constants of the My JiuMi page.
/// </summary>
public static class MyJiuMiInteractionRules
{
    /// <summary>
    /// Gets how long the cat stays awake without interaction before curling down to sleep.
    /// </summary>
    public static TimeSpan SleepDelay { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the shortest random delay between idle blinks.
    /// </summary>
    public static TimeSpan BlinkDelayMin { get; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the longest random delay between idle blinks.
    /// </summary>
    public static TimeSpan BlinkDelayMax { get; } = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Gets the shortest random pause between autonomous wander decisions.
    /// </summary>
    public static TimeSpan WanderDelayMin { get; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets the longest random pause between autonomous wander decisions.
    /// </summary>
    public static TimeSpan WanderDelayMax { get; } = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Gets the smallest distance, in DIP, between the cat and a wander target.
    /// </summary>
    public const double WanderMinDistance = 120;

    /// <summary>
    /// Gets the margin, in DIP, a wander target keeps from the room edges.
    /// </summary>
    public const double WanderEdgeMargin = 40;

    /// <summary>
    /// Gets the walking speed in DIP per second, matched to the 33-frame walk stride so the paws do
    /// not slide.
    /// </summary>
    public const double WalkSpeed = 103;

    /// <summary>
    /// Gets the walking speed in DIP per second used while the desktop cat walks home and the page
    /// cat finishes the summon; 1.6x <see cref="WalkSpeed"/> keeps the stride of the fast frames.
    /// </summary>
    public const double SummonWalkSpeed = 167;

    /// <summary>
    /// Gets the frame interval of the summon walk, matched to <see cref="SummonWalkSpeed"/>.
    /// </summary>
    public const int SummonWalkFrameMilliseconds = 16;

    /// <summary>
    /// Gets the side of the square cat slot, matching the 200 DIP display size.
    /// </summary>
    public const double CatSize = 200;

    /// <summary>
    /// Gets the width, in DIP, of the cat's pointer hit area; the exported cat art is about
    /// 170–177 DIP wide, so this keeps empty floor on either side clickable.
    /// </summary>
    public const double CatHitWidth = 180;

    /// <summary>
    /// Gets the height, in DIP, of the cat's pointer hit area above the floor line; the tallest
    /// exported frame has visible art within the bottom 121 DIP.
    /// </summary>
    public const double CatHitHeight = 140;

    /// <summary>
    /// Gets the horizontal pointer movement, in pixels, that turns a click into a drag.
    /// </summary>
    public const double DragThreshold = 4;

    /// <summary>
    /// Gets how long the heart waits after a click before it appears.
    /// </summary>
    public static TimeSpan HeartDelay { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Gets how long the heart overlay stays visible.
    /// </summary>
    public static TimeSpan HeartDuration { get; } = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// Gets the horizontal distance from the cat's head centre, in DIP, within which the cat looks
    /// forward in <see cref="MyJiuMiState.SitWatch"/>.
    /// </summary>
    public const double SitWatchHorizontalThresholdDip = 60;

    /// <summary>
    /// Gets the hysteresis half-band, in DIP, applied around <see cref="SitWatchHorizontalThresholdDip"/>
    /// to prevent rapid head flipping when the cursor hovers near the boundary.
    /// </summary>
    public const double SitWatchHysteresisDip = 15;

    /// <summary>
    /// Gets the minimum elapsed time between gaze direction changes in <see cref="MyJiuMiState.SitWatch"/>.
    /// </summary>
    public static TimeSpan SitWatchMinSwitchInterval { get; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the cursor displacement, in DIP, that counts as an effective cursor movement and resets
    /// the sit-watch stillness timeout.
    /// </summary>
    public const double SitWatchEffectiveMoveDip = 8;

    /// <summary>
    /// Gets the maximum continuous time the cat keeps watching the cursor before getting tired.
    /// </summary>
    public static TimeSpan SitWatchMaxDuration { get; } = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Gets how long the cursor may remain without effective movement before the sitting cat loses interest.
    /// </summary>
    public static TimeSpan SitWatchIdleTimeout { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets how long the cat rests in <see cref="MyJiuMiState.LyingIdle"/> after lying down from
    /// <see cref="MyJiuMiState.SitWatch"/> before entering <see cref="MyJiuMiState.SleepIn"/>.
    /// </summary>
    public static TimeSpan SitWatchLieSleepBuffer { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets the probability that a page-mode cat lies down to sleep (rather than wandering) when
    /// sit-watch patience runs out.
    /// </summary>
    public const double SitWatchPageSleepProbability = 0.75;

    /// <summary>
    /// Gets the vertical position of the sitting cat's head centre measured upward from the floor
    /// anchor as a fraction of the square cat slot.
    /// </summary>
    public const double SitWatchHeadHeightRatio = 0.6;

    /// <summary>
    /// Classifies an initial horizontal cursor offset relative to the cat's head centre using the
    /// nominal <see cref="SitWatchHorizontalThresholdDip"/> boundaries.
    /// </summary>
    /// <param name="offsetXDip">The cursor X minus the cat head centre X, in DIP.</param>
    /// <returns>The matching gaze direction.</returns>
    public static MyJiuMiSitWatchDirection ClassifySitWatchDirection(double offsetXDip)
    {
        if (!double.IsFinite(offsetXDip))
        {
            return MyJiuMiSitWatchDirection.Center;
        }

        if (offsetXDip < -SitWatchHorizontalThresholdDip)
        {
            return MyJiuMiSitWatchDirection.Left;
        }

        if (offsetXDip > SitWatchHorizontalThresholdDip)
        {
            return MyJiuMiSitWatchDirection.Right;
        }

        return MyJiuMiSitWatchDirection.Center;
    }

    /// <summary>
    /// Resolves the next gaze direction from <paramref name="current"/> by applying the
    /// <see cref="SitWatchHysteresisDip"/> band around <see cref="SitWatchHorizontalThresholdDip"/>.
    /// </summary>
    /// <param name="current">The currently active gaze direction.</param>
    /// <param name="offsetXDip">The cursor X minus the cat head centre X, in DIP.</param>
    /// <returns>The updated gaze direction.</returns>
    public static MyJiuMiSitWatchDirection ResolveSitWatchDirection(
        MyJiuMiSitWatchDirection current,
        double offsetXDip)
    {
        if (!double.IsFinite(offsetXDip))
        {
            return current;
        }

        double enterSide = SitWatchHorizontalThresholdDip + SitWatchHysteresisDip;
        double exitSide = SitWatchHorizontalThresholdDip - SitWatchHysteresisDip;

        return current switch
        {
            MyJiuMiSitWatchDirection.Left => offsetXDip > enterSide
                ? MyJiuMiSitWatchDirection.Right
                : offsetXDip >= -exitSide
                    ? MyJiuMiSitWatchDirection.Center
                    : MyJiuMiSitWatchDirection.Left,
            MyJiuMiSitWatchDirection.Right => offsetXDip < -enterSide
                ? MyJiuMiSitWatchDirection.Left
                : offsetXDip <= exitSide
                    ? MyJiuMiSitWatchDirection.Center
                    : MyJiuMiSitWatchDirection.Right,
            _ => offsetXDip < -enterSide
                ? MyJiuMiSitWatchDirection.Left
                : offsetXDip > enterSide
                    ? MyJiuMiSitWatchDirection.Right
                    : MyJiuMiSitWatchDirection.Center,
        };
    }
}
