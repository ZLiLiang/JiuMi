using System;
using System.Collections.Generic;
using System.Linq;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The persisted state of the world clock: the last view and the selected places.
/// </summary>
public sealed record WorldClockSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockSettings"/> class.
    /// </summary>
    /// <param name="view">The view to open the page in.</param>
    /// <param name="places">The selected places, in display order.</param>
    public WorldClockSettings(WorldClockViewMode view, IReadOnlyList<Place> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        View = view;
        Places = [.. places];
    }

    /// <summary>
    /// Gets the view to open the page in.
    /// </summary>
    public WorldClockViewMode View
    {
        get;
    }

    /// <summary>
    /// Gets the selected places, in display order.
    /// </summary>
    public IReadOnlyList<Place> Places
    {
        get;
    }

    /// <summary>
    /// Creates the settings used before the user has changed anything: four cities in the board view.
    /// </summary>
    /// <returns>The default world clock settings.</returns>
    public static WorldClockSettings CreateDefault() => new(
        WorldClockViewMode.Board,
        [
            new Place(PlaceKind.City, "Asia/Shanghai", WorkWindow.Default),
            new Place(PlaceKind.City, "Europe/London", WorkWindow.Default),
            new Place(PlaceKind.City, "America/New_York", WorkWindow.Default),
            new Place(PlaceKind.City, "America/Los_Angeles", WorkWindow.Default),
        ]);

    /// <summary>
    /// Compares two settings values by value, including the place list.
    /// </summary>
    /// <param name="other">The settings to compare with.</param>
    /// <returns><see langword="true"/> when both have the same view and places.</returns>
    public bool Equals(WorldClockSettings? other) =>
        other is not null && View == other.View && Places.SequenceEqual(other.Places);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(View, Places.Count);
}
