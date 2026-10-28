namespace JiuMi.Application.WorldClock;

/// <summary>
/// One hour cell of one place in the plan grid.
/// </summary>
/// <param name="Status">The state at the start of the hour.</param>
/// <param name="IsNight">Whether the local hour is before 06:00 or from 22:00, used for the darker off colour.</param>
/// <param name="IsDayBreak">Whether this hour contains the local midnight of the place.</param>
public sealed record WorldClockCell(WorldClockStatus Status, bool IsNight, bool IsDayBreak)
{
    /// <summary>
    /// Gets whether the place is working in this hour.
    /// </summary>
    public bool IsWorking => Status == WorldClockStatus.Working;
}
