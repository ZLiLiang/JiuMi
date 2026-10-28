using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The working window of a place: a start time, an end time and the ISO weekdays it applies to.
/// </summary>
public sealed record WorkWindow
{
    /// <summary>
    /// The default window used when no override applies: 09:00–18:00, Monday to Friday.
    /// </summary>
    public static WorkWindow Default { get; } = new(new TimeOnly(9, 0), new TimeOnly(18, 0), [1, 2, 3, 4, 5]);

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkWindow"/> class.
    /// </summary>
    /// <param name="start">The inclusive start time.</param>
    /// <param name="end">The exclusive end time.</param>
    /// <param name="days">The ISO weekdays (1 = Monday … 7 = Sunday) the window applies to.</param>
    public WorkWindow(TimeOnly start, TimeOnly end, IReadOnlyList<int> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (!TryValidate(start, end, days, out string? error))
        {
            throw new ArgumentException(error, nameof(days));
        }

        Start = start;
        End = end;
        Days = [.. days.Distinct().Order()];
    }

    /// <summary>
    /// Gets the inclusive start time.
    /// </summary>
    public TimeOnly Start
    {
        get;
    }

    /// <summary>
    /// Gets the exclusive end time.
    /// </summary>
    public TimeOnly End
    {
        get;
    }

    /// <summary>
    /// Gets the ISO weekdays (1 = Monday … 7 = Sunday), sorted and without duplicates.
    /// </summary>
    public IReadOnlyList<int> Days
    {
        get;
    }

    /// <summary>
    /// Converts a <see cref="DayOfWeek"/> to its ISO weekday number.
    /// </summary>
    /// <param name="day">The day to convert.</param>
    /// <returns>The ISO number, 1 (Monday) to 7 (Sunday).</returns>
    public static int ToIsoDay(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;

    /// <summary>
    /// Tries to build a window from user or data values.
    /// </summary>
    /// <param name="start">The inclusive start time.</param>
    /// <param name="end">The exclusive end time.</param>
    /// <param name="days">The ISO weekdays the window applies to.</param>
    /// <param name="window">The created window, or <see langword="null"/> when the values are invalid.</param>
    /// <param name="error">A diagnostic message when the values are invalid.</param>
    /// <returns><see langword="true"/> when the values form a valid window.</returns>
    public static bool TryCreate(
        TimeOnly start,
        TimeOnly end,
        IReadOnlyList<int> days,
        out WorkWindow? window,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (!TryValidate(start, end, days, out error))
        {
            window = null;
            return false;
        }

        window = new WorkWindow(start, end, days);
        return true;
    }

    /// <summary>
    /// Parses a 24-hour <c>HH:mm</c> value at the fifteen-minute granularity used by the settings.
    /// </summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="time">The parsed time.</param>
    /// <returns><see langword="true"/> when the text is a valid time.</returns>
    public static bool TryParseTime(string? value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(
            value,
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    /// <summary>
    /// Gets whether the window applies to <paramref name="day"/>.
    /// </summary>
    /// <param name="day">The local weekday.</param>
    /// <returns><see langword="true"/> when the window applies to that weekday.</returns>
    public bool IsWorkingDay(DayOfWeek day) => Days.Contains(ToIsoDay(day));

    /// <summary>
    /// Gets whether the window covers the given local day and time.
    /// </summary>
    /// <param name="day">The local weekday.</param>
    /// <param name="time">The local time of day.</param>
    /// <returns><see langword="true"/> when the moment is inside the window.</returns>
    public bool Covers(DayOfWeek day, TimeOnly time) => IsWorkingDay(day) && time >= Start && time < End;

    /// <summary>
    /// Compares two windows by value, including the day list.
    /// </summary>
    /// <param name="other">The window to compare with.</param>
    /// <returns><see langword="true"/> when both windows have the same times and days.</returns>
    public bool Equals(WorkWindow? other) =>
        other is not null &&
        Start == other.Start &&
        End == other.End &&
        Days.SequenceEqual(other.Days);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Start, End, Days.Count);

    private static bool TryValidate(TimeOnly start, TimeOnly end, IReadOnlyList<int> days, out string? error)
    {
        if (start >= end)
        {
            error = "The end time must be later than the start time.";
            return false;
        }

        if (start.Minute % 15 != 0 || end.Minute % 15 != 0)
        {
            error = "The times must land on a fifteen-minute boundary.";
            return false;
        }

        if (days.Count == 0)
        {
            error = "At least one working day is required.";
            return false;
        }

        foreach (int day in days)
        {
            if (day is < 1 or > 7)
            {
                error = "Working days must be ISO numbers between 1 and 7.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
