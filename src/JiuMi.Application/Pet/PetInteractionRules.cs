namespace JiuMi.Application.Pet;

/// <summary>
/// The frame-by-frame loop, timing and overlay rules of the desktop pet.
/// </summary>
public static class PetInteractionRules
{
    /// <summary>
    /// Gets how long the pet stays idle before curling down to sleep.
    /// </summary>
    public static TimeSpan SleepDelay { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the shortest random delay between idle blinks.
    /// </summary>
    public static TimeSpan BlinkDelayMin { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets the longest random delay between idle blinks.
    /// </summary>
    public static TimeSpan BlinkDelayMax { get; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Gets how long the heart waits before it appears after a click.
    /// </summary>
    public static TimeSpan HeartDelay { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Gets how long the heart overlay stays visible after a reaction.
    /// </summary>
    public static TimeSpan HeartDuration { get; } = TimeSpan.FromMilliseconds(800);
}
