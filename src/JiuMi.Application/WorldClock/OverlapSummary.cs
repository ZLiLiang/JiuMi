using System;
using System.Collections.Generic;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The maximum number of places working at the same time and the hours that reach it.
/// </summary>
public sealed class OverlapSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OverlapSummary"/> class.
    /// </summary>
    /// <param name="maxCount">The maximum number of places working in one hour.</param>
    /// <param name="placeCount">The number of available places considered.</param>
    /// <param name="ranges">The hours that reach the maximum; empty when the maximum is below two.</param>
    public OverlapSummary(int maxCount, int placeCount, IReadOnlyList<OverlapRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        MaxCount = maxCount;
        PlaceCount = placeCount;
        Ranges = [.. ranges];
    }

    /// <summary>
    /// Gets the maximum number of places working in one hour.
    /// </summary>
    public int MaxCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of available places considered.
    /// </summary>
    public int PlaceCount
    {
        get;
    }

    /// <summary>
    /// Gets the hours that reach the maximum, or an empty list when fewer than two places overlap.
    /// </summary>
    public IReadOnlyList<OverlapRange> Ranges
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether at least two places work at the same time.
    /// </summary>
    public bool HasOverlap => MaxCount >= 2;

    /// <summary>
    /// Gets a value indicating whether every available place works at the same time.
    /// </summary>
    public bool HasFullOverlap => PlaceCount > 0 && MaxCount == PlaceCount;
}
