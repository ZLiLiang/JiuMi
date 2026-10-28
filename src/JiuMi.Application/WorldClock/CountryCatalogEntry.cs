using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// One bundled country entry: its names, dial code and the representative time zone.
/// </summary>
public sealed record CountryCatalogEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CountryCatalogEntry"/> class.
    /// </summary>
    /// <param name="iso2">The ISO alpha-2 code.</param>
    /// <param name="name">The bilingual country name.</param>
    /// <param name="dialCode">The country calling code, such as <c>+86</c>.</param>
    /// <param name="zone">The IANA time zone the country entry binds to.</param>
    /// <param name="representativeCity">
    /// The bilingual representative city for multi-zone countries; <see langword="null"/> for single-zone countries.
    /// </param>
    public CountryCatalogEntry(
        string iso2,
        WorldClockName name,
        string dialCode,
        string zone,
        WorldClockName? representativeCity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iso2);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(dialCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);

        Iso2 = iso2.ToUpperInvariant();
        Name = name;
        DialCode = dialCode;
        Zone = zone;
        RepresentativeCity = representativeCity;
    }

    /// <summary>
    /// Gets the ISO alpha-2 code.
    /// </summary>
    public string Iso2
    {
        get;
    }

    /// <summary>
    /// Gets the bilingual country name.
    /// </summary>
    public WorldClockName Name
    {
        get;
    }

    /// <summary>
    /// Gets the country calling code, such as <c>+86</c>.
    /// </summary>
    public string DialCode
    {
        get;
    }

    /// <summary>
    /// Gets the IANA time zone the country entry binds to.
    /// </summary>
    public string Zone
    {
        get;
    }

    /// <summary>
    /// Gets the representative city for a multi-zone country, or <see langword="null"/>.
    /// </summary>
    public WorldClockName? RepresentativeCity
    {
        get;
    }
}
