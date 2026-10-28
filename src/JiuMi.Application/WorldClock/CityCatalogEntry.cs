using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// One bundled city entry. Its id is the IANA time zone, so a zone appears at most once.
/// </summary>
public sealed record CityCatalogEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CityCatalogEntry"/> class.
    /// </summary>
    /// <param name="id">The IANA time zone id.</param>
    /// <param name="countryCode">The ISO alpha-2 code of the country the city belongs to.</param>
    /// <param name="name">The bilingual city name.</param>
    public CityCatalogEntry(string id, string countryCode, WorldClockName name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(name);

        Id = id;
        CountryCode = countryCode.ToUpperInvariant();
        Name = name;
    }

    /// <summary>
    /// Gets the IANA time zone id.
    /// </summary>
    public string Id
    {
        get;
    }

    /// <summary>
    /// Gets the ISO alpha-2 code of the country the city belongs to.
    /// </summary>
    public string CountryCode
    {
        get;
    }

    /// <summary>
    /// Gets the bilingual city name.
    /// </summary>
    public WorldClockName Name
    {
        get;
    }
}
