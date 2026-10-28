using System;
using System.Collections.Generic;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The outcome of a holiday lookup.
/// </summary>
public sealed record HolidayLookup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayLookup"/> class.
    /// </summary>
    /// <param name="status">Whether dates were found, the country has no data, or the lookup failed.</param>
    /// <param name="dates">The holiday dates; empty unless the status is available.</param>
    /// <param name="error">The failure, when the status is failed.</param>
    public HolidayLookup(HolidayLookupStatus status, IReadOnlySet<DateOnly> dates, Exception? error = null)
    {
        ArgumentNullException.ThrowIfNull(dates);
        Status = status;
        Dates = dates;
        Error = error;
    }

    /// <summary>
    /// Gets a lookup that found no holiday data for the country and year.
    /// </summary>
    public static HolidayLookup NoData { get; } = new(HolidayLookupStatus.NoData, new HashSet<DateOnly>());

    /// <summary>
    /// Gets whether dates were found, the country has no data, or the lookup failed.
    /// </summary>
    public HolidayLookupStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets the holiday dates.
    /// </summary>
    public IReadOnlySet<DateOnly> Dates
    {
        get;
    }

    /// <summary>
    /// Gets the failure, when the status is failed.
    /// </summary>
    public Exception? Error
    {
        get;
    }
}
