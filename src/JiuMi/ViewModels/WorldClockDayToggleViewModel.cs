using CommunityToolkit.Mvvm.ComponentModel;

namespace JiuMi.ViewModels;

/// <summary>
/// One working-day checkbox in the working-hours editor.
/// </summary>
public sealed class WorldClockDayToggleViewModel : ObservableObject
{
    private bool _isChecked;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockDayToggleViewModel"/> class.
    /// </summary>
    /// <param name="isoDay">The ISO weekday, 1 (Monday) to 7 (Sunday).</param>
    /// <param name="label">The localized day name.</param>
    /// <param name="isChecked">Whether the day is a working day.</param>
    public WorldClockDayToggleViewModel(int isoDay, string label, bool isChecked)
    {
        IsoDay = isoDay;
        Label = label;
        _isChecked = isChecked;
    }

    /// <summary>
    /// Gets the ISO weekday.
    /// </summary>
    public int IsoDay
    {
        get;
    }

    /// <summary>
    /// Gets the localized day name.
    /// </summary>
    public string Label
    {
        get;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the day is a working day.
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}
