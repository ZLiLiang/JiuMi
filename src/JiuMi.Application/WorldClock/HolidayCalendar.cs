using System;
using System.Collections.Generic;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The public holidays of the countries in use, keyed by country and local date.
/// </summary>
public sealed class HolidayCalendar
{
    private readonly Dictionary<string, IReadOnlySet<DateOnly>> _dates;

    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayCalendar"/> class.
    /// </summary>
    /// <param name="dates">The holiday dates per ISO alpha-2 country code.</param>
    public HolidayCalendar(IReadOnlyDictionary<string, IReadOnlySet<DateOnly>> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var copy = new Dictionary<string, IReadOnlySet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IReadOnlySet<DateOnly>> pair in dates)
        {
            copy[pair.Key] = pair.Value;
        }

        _dates = copy;
    }

    /// <summary>
    /// Gets a calendar with no holidays.
    /// </summary>
    public static HolidayCalendar Empty { get; } = new(new Dictionary<string, IReadOnlySet<DateOnly>>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Gets whether a country has a public holiday on a local date.
    /// </summary>
    /// <param name="countryCode">The ISO alpha-2 code, or <see langword="null"/> when unknown.</param>
    /// <param name="date">The local date.</param>
    /// <returns><see langword="true"/> when the date is a public holiday.</returns>
    public bool IsHoliday(string? countryCode, DateOnly date)
    {
        return countryCode is not null &&
            _dates.TryGetValue(countryCode, out IReadOnlySet<DateOnly>? dates) &&
            dates.Contains(date);
    }
}
