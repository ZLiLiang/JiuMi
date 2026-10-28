using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The texts and data needed to show one place in the board or the plan.
/// </summary>
public sealed record PlacePresentation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlacePresentation"/> class.
    /// </summary>
    /// <param name="place">The place being shown.</param>
    /// <param name="primaryName">The name in the interface language, with the representative city where applicable.</param>
    /// <param name="secondaryName">The name in the other language.</param>
    /// <param name="dialCode">The country calling code, or an empty string when the catalog cannot resolve it.</param>
    /// <param name="zoneId">The IANA time zone id used for this place.</param>
    /// <param name="hours">The working window of the place.</param>
    public PlacePresentation(
        Place place,
        string primaryName,
        string secondaryName,
        string dialCode,
        string zoneId,
        WorkWindow hours)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryName);
        ArgumentNullException.ThrowIfNull(secondaryName);
        ArgumentNullException.ThrowIfNull(dialCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentNullException.ThrowIfNull(hours);

        Place = place;
        PrimaryName = primaryName;
        SecondaryName = secondaryName;
        DialCode = dialCode;
        ZoneId = zoneId;
        Hours = hours;
    }

    /// <summary>
    /// Gets the place being shown.
    /// </summary>
    public Place Place
    {
        get;
    }

    /// <summary>
    /// Gets the name in the interface language.
    /// </summary>
    public string PrimaryName
    {
        get;
    }

    /// <summary>
    /// Gets the name in the other language.
    /// </summary>
    public string SecondaryName
    {
        get;
    }

    /// <summary>
    /// Gets the country calling code, or an empty string when unknown.
    /// </summary>
    public string DialCode
    {
        get;
    }

    /// <summary>
    /// Gets the IANA time zone id used for this place.
    /// </summary>
    public string ZoneId
    {
        get;
    }

    /// <summary>
    /// Gets the working window of the place.
    /// </summary>
    public WorkWindow Hours
    {
        get;
    }
}
