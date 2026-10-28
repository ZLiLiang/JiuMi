namespace JiuMi.Application.WorldClock;

/// <summary>
/// The two views of the world clock page.
/// </summary>
public enum WorldClockViewMode
{
    /// <summary>One row per place with its local time and status.</summary>
    Board,

    /// <summary>A 24-hour grid with an overlap band and a cursor.</summary>
    Plan,
}
