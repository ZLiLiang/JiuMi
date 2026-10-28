namespace JiuMi.Application.WorldClock;

/// <summary>
/// The granularity of a place in the world clock.
/// </summary>
public enum PlaceKind
{
    /// <summary>A country bound to one representative time zone.</summary>
    Country,

    /// <summary>A city bound to its IANA time zone.</summary>
    City,
}
