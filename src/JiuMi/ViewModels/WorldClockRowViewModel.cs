using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using JiuMi.Application.Localization;
using JiuMi.Application.WorldClock;

namespace JiuMi.ViewModels;

/// <summary>
/// One place row in the board and the plan.
/// </summary>
public sealed class WorldClockRowViewModel : ObservableObject
{
    private readonly WorldClockRow _row;
    private readonly ILocalizationService _localization;
    private string _localTimeText = "--:--";
    private string _weekdayText = string.Empty;
    private WorldClockStatus _status = WorldClockStatus.Unavailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockRowViewModel"/> class.
    /// </summary>
    /// <param name="row">The computed row.</param>
    /// <param name="localization">Supplies status and weekday text.</param>
    /// <param name="isLast">Whether this row is last, which removes the divider.</param>
    public WorldClockRowViewModel(WorldClockRow row, ILocalizationService localization, bool isLast)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(localization);
        _row = row;
        _localization = localization;
        IsLast = isLast;

        var cells = new WorldClockCellViewModel[row.Cells.Count];
        for (int index = 0; index < row.Cells.Count; index++)
        {
            cells[index] = new WorldClockCellViewModel(row.Cells[index]);
        }

        Cells = cells;
    }

    /// <summary>
    /// Gets the name in the interface language.
    /// </summary>
    public string PrimaryName => _row.Presentation.PrimaryName;

    /// <summary>
    /// Gets the name in the other language.
    /// </summary>
    public string SecondaryName => _row.Presentation.SecondaryName;

    /// <summary>
    /// Gets the country calling code.
    /// </summary>
    public string DialCode => _row.Presentation.DialCode;

    /// <summary>
    /// Gets a value indicating whether the row has a dial code to show.
    /// </summary>
    public bool HasDialCode => !string.IsNullOrEmpty(DialCode);

    /// <summary>
    /// Gets the twenty-four hour cells.
    /// </summary>
    public IReadOnlyList<WorldClockCellViewModel> Cells
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the time zone of the place can be resolved.
    /// </summary>
    public bool IsAvailable => _row.IsAvailable;

    /// <summary>
    /// Gets a value indicating whether this is the last row.
    /// </summary>
    public bool IsLast
    {
        get;
    }

    /// <summary>
    /// Gets the local time at the instant the labels show.
    /// </summary>
    public string LocalTimeText
    {
        get => _localTimeText;
        private set => SetProperty(ref _localTimeText, value);
    }

    /// <summary>
    /// Gets the local weekday at the instant the labels show.
    /// </summary>
    public string WeekdayText
    {
        get => _weekdayText;
        private set => SetProperty(ref _weekdayText, value);
    }

    /// <summary>
    /// Gets the time and weekday as one line, or the unavailable text.
    /// </summary>
    public string LocalTimeLine => string.IsNullOrEmpty(WeekdayText)
        ? LocalTimeText
        : $"{LocalTimeText} {WeekdayText}";

    /// <summary>
    /// Gets the status at the instant the labels show.
    /// </summary>
    public WorldClockStatus Status
    {
        get => _status;
        private set
        {
            if (!SetProperty(ref _status, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsWorkingStatus));
            OnPropertyChanged(nameof(IsHolidayStatus));
            OnPropertyChanged(nameof(IsWeekendStatus));
            OnPropertyChanged(nameof(IsUnavailableStatus));
        }
    }

    /// <summary>
    /// Gets the localized status text.
    /// </summary>
    public string StatusText => _status switch
    {
        WorldClockStatus.Working => _localization["Tool.WorldClock.Status.Working"],
        WorldClockStatus.Off => _localization["Tool.WorldClock.Status.Off"],
        WorldClockStatus.Weekend => _localization["Tool.WorldClock.Status.Weekend"],
        WorldClockStatus.Holiday => _localization["Tool.WorldClock.Status.Holiday"],
        _ => _localization["Tool.WorldClock.Status.Unavailable"],
    };

    /// <summary>
    /// Gets a value indicating whether the status is working.
    /// </summary>
    public bool IsWorkingStatus => _status == WorldClockStatus.Working;

    /// <summary>
    /// Gets a value indicating whether the status is a public holiday.
    /// </summary>
    public bool IsHolidayStatus => _status == WorldClockStatus.Holiday;

    /// <summary>
    /// Gets a value indicating whether the status is a weekend.
    /// </summary>
    public bool IsWeekendStatus => _status == WorldClockStatus.Weekend;

    /// <summary>
    /// Gets a value indicating whether the time zone is unavailable.
    /// </summary>
    public bool IsUnavailableStatus => _status == WorldClockStatus.Unavailable;

    /// <summary>
    /// Updates the labels for an absolute instant.
    /// </summary>
    /// <param name="instant">The instant the page currently shows.</param>
    /// <param name="holidays">The holidays known for the place's country.</param>
    public void Update(DateTimeOffset instant, HolidayCalendar holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        WorldClockLocalTime local = _row.GetLocalTime(instant, holidays);
        Status = local.Status;
        if (!_row.IsAvailable)
        {
            LocalTimeText = "--:--";
            WeekdayText = string.Empty;
            OnPropertyChanged(nameof(LocalTimeLine));
            return;
        }

        LocalTimeText = local.LocalDateTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        WeekdayText = WeekdayName(local.LocalDateTime.DayOfWeek);
        OnPropertyChanged(nameof(LocalTimeLine));
    }

    private string WeekdayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => _localization["Tool.WorldClock.Weekday.Mon"],
        DayOfWeek.Tuesday => _localization["Tool.WorldClock.Weekday.Tue"],
        DayOfWeek.Wednesday => _localization["Tool.WorldClock.Weekday.Wed"],
        DayOfWeek.Thursday => _localization["Tool.WorldClock.Weekday.Thu"],
        DayOfWeek.Friday => _localization["Tool.WorldClock.Weekday.Fri"],
        DayOfWeek.Saturday => _localization["Tool.WorldClock.Weekday.Sat"],
        _ => _localization["Tool.WorldClock.Weekday.Sun"],
    };
}
