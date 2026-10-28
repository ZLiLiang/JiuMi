using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Applies the holiday > weekend > working > off precedence for one place and instant.
/// </summary>
internal static class WorldClockStatusResolver
{
    public static WorldClockStatus Resolve(
        WorkWindow hours,
        string? countryCode,
        TimeZoneInfo zone,
        DateTimeOffset instant,
        HolidayCalendar holidays,
        out DateTime local,
        out bool isNight,
        out bool isDayBreak)
    {
        ArgumentNullException.ThrowIfNull(hours);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(holidays);

        local = TimeZoneInfo.ConvertTime(instant, zone).DateTime;
        int hour = local.Hour;
        isNight = hour < 6 || hour >= 22;
        isDayBreak = hour == 0;

        if (holidays.IsHoliday(countryCode, DateOnly.FromDateTime(local)))
        {
            return WorldClockStatus.Holiday;
        }

        if (!hours.IsWorkingDay(local.DayOfWeek))
        {
            return WorldClockStatus.Weekend;
        }

        return hours.Covers(local.DayOfWeek, TimeOnly.FromDateTime(local))
            ? WorldClockStatus.Working
            : WorldClockStatus.Off;
    }
}
