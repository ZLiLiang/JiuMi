using System;
using System.Collections.Generic;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The computed view of the next twenty-four hours in the reference time zone.
/// </summary>
public sealed class WorldClockTimeline
{
    /// <summary>
    /// The number of hour columns in the plan grid.
    /// </summary>
    public const int ColumnCount = 24;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockTimeline"/> class.
    /// </summary>
    /// <param name="start">The absolute instant of the reference zone's local midnight.</param>
    /// <param name="referenceZone">The zone whose day the timeline follows; normally the machine's zone.</param>
    /// <param name="columns">The twenty-four hour columns.</param>
    /// <param name="rows">The place rows, in display order.</param>
    /// <param name="overlap">The overlap conclusion.</param>
    public WorldClockTimeline(
        DateTimeOffset start,
        TimeZoneInfo referenceZone,
        IReadOnlyList<WorldClockColumn> columns,
        IReadOnlyList<WorldClockRow> rows,
        OverlapSummary overlap)
    {
        ArgumentNullException.ThrowIfNull(referenceZone);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(overlap);
        Start = start;
        ReferenceZone = referenceZone;
        Columns = [.. columns];
        Rows = [.. rows];
        Overlap = overlap;
    }

    /// <summary>
    /// Gets the absolute instant of the reference zone's local midnight.
    /// </summary>
    public DateTimeOffset Start
    {
        get;
    }

    /// <summary>
    /// Gets the zone whose day the timeline follows.
    /// </summary>
    public TimeZoneInfo ReferenceZone
    {
        get;
    }

    /// <summary>
    /// Gets the twenty-four hour columns.
    /// </summary>
    public IReadOnlyList<WorldClockColumn> Columns
    {
        get;
    }

    /// <summary>
    /// Gets the place rows, in display order.
    /// </summary>
    public IReadOnlyList<WorldClockRow> Rows
    {
        get;
    }

    /// <summary>
    /// Gets the overlap conclusion.
    /// </summary>
    public OverlapSummary Overlap
    {
        get;
    }

    /// <summary>
    /// Gets the absolute instant at a cursor position measured from midnight.
    /// </summary>
    /// <param name="minutesSinceMidnight">Minutes after the reference midnight, 0 to 1425.</param>
    /// <returns>The absolute instant.</returns>
    public DateTimeOffset InstantAt(int minutesSinceMidnight) => Start.AddMinutes(minutesSinceMidnight);

    /// <summary>
    /// Gets the reference zone's local time at an absolute instant.
    /// </summary>
    /// <param name="instant">The absolute instant.</param>
    /// <returns>The wall-clock time in the reference zone.</returns>
    public DateTime GetReferenceLocalTime(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, ReferenceZone).DateTime;
}
