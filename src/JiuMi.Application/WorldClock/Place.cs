using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// One selected place in the world clock: a country entry or a city entry with its own window.
/// </summary>
public sealed record Place
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Place"/> class.
    /// </summary>
    /// <param name="kind">The granularity of the place.</param>
    /// <param name="id">The ISO alpha-2 country code or the IANA city id.</param>
    /// <param name="hours">The working window of this place.</param>
    public Place(PlaceKind kind, string id, WorkWindow hours)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(hours);
        Id = id;
        Kind = kind;
        Hours = hours;
    }

    /// <summary>
    /// Gets the granularity of the place.
    /// </summary>
    public PlaceKind Kind
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the ISO alpha-2 country code for country places, or the IANA time zone id for city places.
    /// </summary>
    public string Id
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the working window of this place.
    /// </summary>
    public WorkWindow Hours
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the stable key of the place across the catalog and the selected list.
    /// </summary>
    public string Key => Kind == PlaceKind.Country ? $"country:{Id}" : $"city:{Id}";

    /// <summary>
    /// Tries to build a place from stored values.
    /// </summary>
    /// <param name="kind">The stored kind, <c>country</c> or <c>city</c>.</param>
    /// <param name="id">The stored id.</param>
    /// <param name="start">The stored start time.</param>
    /// <param name="end">The stored end time.</param>
    /// <param name="days">The stored ISO weekdays.</param>
    /// <param name="place">The created place, or <see langword="null"/> when the values are invalid.</param>
    /// <param name="error">A diagnostic message when the values are invalid.</param>
    /// <returns><see langword="true"/> when the values form a valid place.</returns>
    public static bool TryCreate(
        string? kind,
        string? id,
        string? start,
        string? end,
        System.Collections.Generic.IReadOnlyList<int>? days,
        out Place? place,
        out string? error)
    {
        place = null;

        PlaceKind parsedKind;
        if (string.Equals(kind, "country", StringComparison.OrdinalIgnoreCase))
        {
            parsedKind = PlaceKind.Country;
        }
        else if (string.Equals(kind, "city", StringComparison.OrdinalIgnoreCase))
        {
            parsedKind = PlaceKind.City;
        }
        else
        {
            error = "The place kind must be 'country' or 'city'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            error = "The place id is required.";
            return false;
        }

        string trimmedId = id.Trim();
        if (parsedKind == PlaceKind.Country &&
            (trimmedId.Length != 2 ||
             !char.IsAsciiLetter(trimmedId[0]) ||
             !char.IsAsciiLetter(trimmedId[1])))
        {
            error = "A country place id must be an ISO alpha-2 code.";
            return false;
        }

        if (parsedKind == PlaceKind.City && !trimmedId.Contains('/', StringComparison.Ordinal))
        {
            error = "A city place id must be an IANA time zone id.";
            return false;
        }

        if (days is null)
        {
            error = "The working days are required.";
            return false;
        }

        if (!WorkWindow.TryParseTime(start, out TimeOnly parsedStart) ||
            !WorkWindow.TryParseTime(end, out TimeOnly parsedEnd))
        {
            error = "The working window must use HH:mm values.";
            return false;
        }

        if (!WorkWindow.TryCreate(parsedStart, parsedEnd, days, out WorkWindow? hours, out error) ||
            hours is null)
        {
            return false;
        }

        place = new Place(
            parsedKind,
            parsedKind == PlaceKind.Country ? trimmedId.ToUpperInvariant() : trimmedId,
            hours);
        return true;
    }
}
