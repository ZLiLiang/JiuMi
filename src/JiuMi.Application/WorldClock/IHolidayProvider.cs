using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Fetches the public holidays of a country and year.
/// </summary>
public interface IHolidayProvider
{
    /// <summary>
    /// Gets the holidays of a country and year, from the network or the cache.
    /// </summary>
    /// <param name="countryCode">The ISO alpha-2 code.</param>
    /// <param name="year">The year to load.</param>
    /// <param name="allowNetwork">
    /// Whether a request may be made. When <see langword="false"/>, only the cache is read, whatever its age.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The lookup outcome; failures never throw.</returns>
    Task<HolidayLookup> GetHolidaysAsync(
        string countryCode,
        int year,
        bool allowNetwork,
        CancellationToken cancellationToken = default);
}
