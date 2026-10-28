namespace JiuMi.Application.WorldClock;

/// <summary>
/// The possible outcomes of a holiday lookup.
/// </summary>
public enum HolidayLookupStatus
{
    /// <summary>Holiday dates were found.</summary>
    Available,

    /// <summary>The service answered that the country and year have no data.</summary>
    NoData,

    /// <summary>The request failed and no cache was available.</summary>
    Failed,

    /// <summary>Online data was disabled in the settings.</summary>
    Disabled,
}
