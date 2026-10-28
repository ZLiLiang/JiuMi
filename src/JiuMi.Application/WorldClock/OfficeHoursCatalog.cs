using System;
using System.Collections.Generic;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Resolves the default working window through the city → country → global chain.
/// </summary>
public sealed class OfficeHoursCatalog
{
    private readonly IReadOnlyList<OfficeHoursOverride> _overrides;
    private readonly Dictionary<string, WorkWindow> _cityHours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkWindow> _countryHours = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="OfficeHoursCatalog"/> class.
    /// </summary>
    /// <param name="global">The window used when no override applies.</param>
    /// <param name="overrides">The country and city overrides; a city entry wins over a country entry.</param>
    public OfficeHoursCatalog(WorkWindow global, IReadOnlyList<OfficeHoursOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(overrides);

        Global = global;
        _overrides = [.. overrides];

        // A later override replaces an earlier one for the same target.
        foreach (OfficeHoursOverride entry in _overrides)
        {
            if (entry.CityId is { } cityId)
            {
                _cityHours[cityId] = entry.Hours;
            }
            else if (entry.CountryCode is { } countryCode)
            {
                _countryHours[countryCode] = entry.Hours;
            }
        }
    }

    /// <summary>
    /// Gets the global default window.
    /// </summary>
    public WorkWindow Global
    {
        get;
    }

    /// <summary>
    /// Gets the overrides in catalog order.
    /// </summary>
    public IReadOnlyList<OfficeHoursOverride> Overrides => _overrides;

    /// <summary>
    /// Resolves the default window for a place.
    /// </summary>
    /// <param name="countryCode">The ISO alpha-2 code, or <see langword="null"/> when unknown.</param>
    /// <param name="cityId">The IANA id for a city, or <see langword="null"/> for a country or unknown place.</param>
    /// <returns>The most specific window found.</returns>
    public WorkWindow Resolve(string? countryCode, string? cityId)
    {
        if (cityId is not null && _cityHours.TryGetValue(cityId, out WorkWindow? cityHours))
        {
            return cityHours;
        }

        if (countryCode is not null && _countryHours.TryGetValue(countryCode, out WorkWindow? countryHours))
        {
            return countryHours;
        }

        return Global;
    }
}
