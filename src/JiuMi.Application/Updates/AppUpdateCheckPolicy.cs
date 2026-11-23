namespace JiuMi.Application.Updates;

/// <summary>
/// Defines the startup delay and 24-hour throttle interval for background update checks.
/// </summary>
public static class AppUpdateCheckPolicy
{
    /// <summary>
    /// Gets the delay after the shell becomes visible before the first background check runs.
    /// </summary>
    public static TimeSpan StartupDelay
    {
        get;
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the shortest interval between two automatic background update checks.
    /// </summary>
    public static TimeSpan Interval
    {
        get;
    } = TimeSpan.FromHours(24);

    /// <summary>
    /// Answers whether a background update check is due at <paramref name="now"/>.
    /// </summary>
    /// <param name="lastCheckedAt">When the previous check succeeded, or <see langword="null"/> if never checked.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns><see langword="true"/> when no check has run or at least <see cref="Interval"/> has elapsed.</returns>
    public static bool IsDue(DateTimeOffset? lastCheckedAt, DateTimeOffset now) =>
        lastCheckedAt is not { } last || now - last >= Interval;

    /// <summary>
    /// Computes the one-shot delay until the next scheduled background check.
    /// </summary>
    /// <param name="lastCheckedAt">When the previous check succeeded, or <see langword="null"/>.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The delay until the next check, clamped to <see cref="Interval"/>.</returns>
    public static TimeSpan GetNextDelay(DateTimeOffset? lastCheckedAt, DateTimeOffset now)
    {
        if (lastCheckedAt is not { } last)
        {
            return Interval;
        }

        TimeSpan elapsed = now - last;
        if (elapsed < TimeSpan.Zero || elapsed >= Interval)
        {
            return Interval;
        }

        return Interval - elapsed;
    }
}
