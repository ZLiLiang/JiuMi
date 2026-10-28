using System;

namespace JiuMi.ViewModels;

/// <summary>
/// One column of the overlap band.
/// </summary>
public sealed class WorldClockBandCellViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockBandCellViewModel"/> class.
    /// </summary>
    /// <param name="workingCount">How many places are working in the hour.</param>
    /// <param name="placeCount">The number of available places.</param>
    public WorldClockBandCellViewModel(int workingCount, int placeCount)
    {
        IsZero = workingCount == 0;
        Opacity = IsZero ? 1 : 0.14 + (workingCount / Math.Max(1.0, placeCount) * 0.6);
    }

    /// <summary>
    /// Gets a value indicating whether no place is working in the hour.
    /// </summary>
    public bool IsZero
    {
        get;
    }

    /// <summary>
    /// Gets the opacity the accent fill uses for this hour.
    /// </summary>
    public double Opacity
    {
        get;
    }
}
