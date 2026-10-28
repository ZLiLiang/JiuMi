using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Decides when the background catalog check runs.
/// </summary>
public static class WorldClockCatalogUpdatePolicy
{
    /// <summary>
    /// Gets the shortest interval between two automatic checks.
    /// </summary>
    public static TimeSpan Interval
    {
        get;
    } = TimeSpan.FromHours(24);

    /// <summary>
    /// Answers whether an automatic check is due.
    /// </summary>
    /// <param name="lastCheckedAt">When the previous check ran, or <see langword="null"/> when it never ran.</param>
    /// <param name="now">The current instant.</param>
    /// <returns><see langword="true"/> when the interval has passed or no check has run.</returns>
    public static bool IsDue(DateTimeOffset? lastCheckedAt, DateTimeOffset now) =>
        lastCheckedAt is not { } last || now - last >= Interval;
}
