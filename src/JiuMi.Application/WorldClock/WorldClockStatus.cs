namespace JiuMi.Application.WorldClock;

/// <summary>
/// The state of a place at one instant of the timeline.
/// </summary>
public enum WorldClockStatus
{
    /// <summary>Inside the working window of a working day.</summary>
    Working,

    /// <summary>Outside the working window of a working day.</summary>
    Off,

    /// <summary>A day the working week does not include.</summary>
    Weekend,

    /// <summary>A public holiday, which overrides both working and weekend days.</summary>
    Holiday,

    /// <summary>The time zone could not be resolved, so no status can be computed.</summary>
    Unavailable,
}
