using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// One place row of the plan grid.
/// </summary>
public sealed class WorldClockRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockRow"/> class.
    /// </summary>
    /// <param name="presentation">The texts and data of the place.</param>
    /// <param name="countryCode">The ISO alpha-2 code used for holidays, or <see langword="null"/>.</param>
    /// <param name="zone">The time zone of the place, or <see langword="null"/> when it cannot be resolved.</param>
    /// <param name="cells">The twenty-four hour cells.</param>
    public WorldClockRow(
        PlacePresentation presentation,
        string? countryCode,
        TimeZoneInfo? zone,
        System.Collections.Generic.IReadOnlyList<WorldClockCell> cells)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(cells);
        Presentation = presentation;
        CountryCode = countryCode;
        Zone = zone;
        Cells = cells;
    }

    /// <summary>
    /// Gets the texts and data of the place.
    /// </summary>
    public PlacePresentation Presentation
    {
        get;
    }

    /// <summary>
    /// Gets the ISO alpha-2 code used for holidays.
    /// </summary>
    public string? CountryCode
    {
        get;
    }

    /// <summary>
    /// Gets the time zone of the place, or <see langword="null"/> when it cannot be resolved.
    /// </summary>
    public TimeZoneInfo? Zone
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the row can be computed.
    /// </summary>
    public bool IsAvailable => Zone is not null;

    /// <summary>
    /// Gets the twenty-four hour cells.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<WorldClockCell> Cells
    {
        get;
    }

    /// <summary>
    /// Gets the local time and status of the place at an absolute instant.
    /// </summary>
    /// <param name="instant">The absolute instant.</param>
    /// <param name="holidays">The holiday calendar used for the place's country.</param>
    /// <returns>The local time and status.</returns>
    public WorldClockLocalTime GetLocalTime(DateTimeOffset instant, HolidayCalendar holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        if (Zone is null)
        {
            return new WorldClockLocalTime(default, WorldClockStatus.Unavailable);
        }

        WorldClockStatus status = WorldClockStatusResolver.Resolve(
            Presentation.Hours,
            CountryCode,
            Zone,
            instant,
            holidays,
            out DateTime local,
            out _,
            out _);
        return new WorldClockLocalTime(local, status);
    }
}
