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
}
