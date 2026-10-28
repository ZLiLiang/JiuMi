using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// A country- or city-level office hours override from the bundled catalog.
/// </summary>
public sealed record OfficeHoursOverride
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OfficeHoursOverride"/> class.
    /// </summary>
    /// <param name="countryCode">The ISO alpha-2 code for a country override, or <see langword="null"/>.</param>
    /// <param name="cityId">The IANA id for a city override, or <see langword="null"/>.</param>
    /// <param name="hours">The working window of the override.</param>
    /// <param name="source">Where the override value was taken from.</param>
    /// <param name="updatedAt">The date the source was last checked.</param>
    public OfficeHoursOverride(
        string? countryCode,
        string? cityId,
        WorkWindow hours,
        string source,
        DateOnly updatedAt)
    {
        ArgumentNullException.ThrowIfNull(hours);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if ((countryCode is null) == (cityId is null))
        {
            throw new ArgumentException("An override must target exactly one of a country or a city.");
        }

        CountryCode = countryCode?.ToUpperInvariant();
        CityId = cityId;
        Hours = hours;
        Source = source;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Gets the ISO alpha-2 code of a country override, or <see langword="null"/> for a city override.
    /// </summary>
    public string? CountryCode
    {
        get;
    }

    /// <summary>
    /// Gets the IANA id of a city override, or <see langword="null"/> for a country override.
    /// </summary>
    public string? CityId
    {
        get;
    }

    /// <summary>
    /// Gets the working window of the override.
    /// </summary>
    public WorkWindow Hours
    {
        get;
    }

    /// <summary>
    /// Gets where the override value was taken from.
    /// </summary>
    public string Source
    {
        get;
    }

    /// <summary>
    /// Gets the date the source was last checked.
    /// </summary>
    public DateOnly UpdatedAt
    {
        get;
    }
}
