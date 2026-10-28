using System;
using System.Collections.Generic;
using System.Linq;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Builds the board and plan data from the selected places, the catalog and the holidays.
/// </summary>
public static class WorldClockCalculator
{
    /// <summary>
    /// Builds the timeline for the next twenty-four hours of the reference zone.
    /// </summary>
    /// <param name="catalog">The bundled catalog used to resolve names, zones and dial codes.</param>
    /// <param name="places">The selected places, in display order.</param>
    /// <param name="languageCode">The concrete interface language.</param>
    /// <param name="now">The current instant used to pick the reference day.</param>
    /// <param name="referenceZone">The zone whose today the grid follows.</param>
    /// <param name="holidays">The holidays known for the selected countries.</param>
    /// <returns>The computed timeline.</returns>
    public static WorldClockTimeline Build(
        WorldClockCatalog catalog,
        IReadOnlyList<Place> places,
        string languageCode,
        DateTimeOffset now,
        TimeZoneInfo referenceZone,
        HolidayCalendar holidays)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(referenceZone);
        ArgumentNullException.ThrowIfNull(holidays);

        DateTime referenceLocal = TimeZoneInfo.ConvertTime(now, referenceZone).DateTime;
        DateTime midnight = referenceLocal.Date;
        var start = new DateTimeOffset(midnight, referenceZone.GetUtcOffset(midnight));

        var rows = new List<WorldClockRow>(places.Count);
        foreach (Place place in places)
        {
            rows.Add(BuildRow(catalog, place, languageCode, start, holidays));
        }

        var columns = new List<WorldClockColumn>(WorldClockTimeline.ColumnCount);
        for (int hour = 0; hour < WorldClockTimeline.ColumnCount; hour++)
        {
            int workingCount = 0;
            foreach (WorldClockRow row in rows)
            {
                if (row.IsAvailable && row.Cells[hour].IsWorking)
                {
                    workingCount++;
                }
            }

            columns.Add(new WorldClockColumn(start.AddHours(hour), workingCount));
        }

        OverlapSummary overlap = BuildOverlap(columns, rows.Count(row => row.IsAvailable));
        return new WorldClockTimeline(start, referenceZone, columns, rows, overlap);
    }

    private static WorldClockRow BuildRow(
        WorldClockCatalog catalog,
        Place place,
        string languageCode,
        DateTimeOffset start,
        HolidayCalendar holidays)
    {
        PlacePresentation? presentation = null;
        string? countryCode = null;
        if (catalog.TryPresent(place, languageCode, out PlacePresentation? resolved) && resolved is not null)
        {
            presentation = resolved;
            countryCode = catalog.GetCountryCode(place);
        }
        else
        {
            // A place that outlived the catalog still gets a row; an unresolvable zone degrades it in place.
            string zoneId = place.Kind == PlaceKind.City ? place.Id : "UTC";
            presentation = new PlacePresentation(place, place.Id, place.Id, string.Empty, zoneId, place.Hours);
        }

        TimeZoneInfo? zone = TryFindZone(presentation.ZoneId);
        var cells = new List<WorldClockCell>(WorldClockTimeline.ColumnCount);
        for (int hour = 0; hour < WorldClockTimeline.ColumnCount; hour++)
        {
            if (zone is null)
            {
                cells.Add(new WorldClockCell(WorldClockStatus.Unavailable, IsNight: false, IsDayBreak: false));
                continue;
            }

            WorldClockStatus status = WorldClockStatusResolver.Resolve(
                presentation.Hours,
                countryCode,
                zone,
                start.AddHours(hour),
                holidays,
                out _,
                out bool isNight,
                out bool isDayBreak);
            cells.Add(new WorldClockCell(status, isNight, isDayBreak));
        }

        return new WorldClockRow(presentation, countryCode, zone, cells);
    }

    private static OverlapSummary BuildOverlap(
        List<WorldClockColumn> columns,
        int availablePlaceCount)
    {
        int maxCount = 0;
        foreach (WorldClockColumn column in columns)
        {
            maxCount = Math.Max(maxCount, column.WorkingCount);
        }

        var ranges = new List<OverlapRange>();
        if (maxCount >= 2)
        {
            int rangeStart = -1;
            for (int hour = 0; hour < columns.Count; hour++)
            {
                bool atMaximum = columns[hour].WorkingCount == maxCount;
                if (atMaximum && rangeStart < 0)
                {
                    rangeStart = hour;
                }
                else if (!atMaximum && rangeStart >= 0)
                {
                    ranges.Add(new OverlapRange(rangeStart, hour));
                    rangeStart = -1;
                }
            }

            if (rangeStart >= 0)
            {
                ranges.Add(new OverlapRange(rangeStart, columns.Count));
            }
        }

        return new OverlapSummary(maxCount, availablePlaceCount, ranges);
    }

    private static TimeZoneInfo? TryFindZone(string zoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
