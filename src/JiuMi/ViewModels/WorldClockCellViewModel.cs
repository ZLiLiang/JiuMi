using JiuMi.Application.WorldClock;

namespace JiuMi.ViewModels;

/// <summary>
/// One hour cell of the plan grid.
/// </summary>
public sealed class WorldClockCellViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockCellViewModel"/> class.
    /// </summary>
    /// <param name="cell">The computed cell.</param>
    public WorldClockCellViewModel(WorldClockCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        Status = cell.Status;
        IsNight = cell.IsNight && cell.Status == WorldClockStatus.Off;
        IsDayBreak = cell.IsDayBreak;
    }

    /// <summary>
    /// Gets the status of the cell.
    /// </summary>
    public WorldClockStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the place is working in this hour.
    /// </summary>
    public bool IsWorking => Status == WorldClockStatus.Working;

    /// <summary>
    /// Gets a value indicating whether the cell is a weekend cell.
    /// </summary>
    public bool IsWeekend => Status == WorldClockStatus.Weekend;

    /// <summary>
    /// Gets a value indicating whether the cell is a public holiday.
    /// </summary>
    public bool IsHoliday => Status == WorldClockStatus.Holiday;

    /// <summary>
    /// Gets a value indicating whether the time zone is unavailable.
    /// </summary>
    public bool IsUnavailable => Status == WorldClockStatus.Unavailable;

    /// <summary>
    /// Gets a value indicating whether the local hour is late or early.
    /// </summary>
    public bool IsNight
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether this hour contains the place's local midnight.
    /// </summary>
    public bool IsDayBreak
    {
        get;
    }
}
