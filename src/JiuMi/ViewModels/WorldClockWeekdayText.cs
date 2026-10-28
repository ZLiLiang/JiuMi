using System;
using JiuMi.Application.Localization;

namespace JiuMi.ViewModels;

/// <summary>
/// Maps weekdays to their localization keys for the world clock labels and editor.
/// </summary>
internal static class WorldClockWeekdayText
{
    public static string Resolve(ILocalizationService localization, DayOfWeek day) =>
        localization[Key(day)];

    public static string Resolve(ILocalizationService localization, int isoDay) =>
        localization[Key(isoDay)];

    private static string Key(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Tool.WorldClock.Weekday.Mon",
        DayOfWeek.Tuesday => "Tool.WorldClock.Weekday.Tue",
        DayOfWeek.Wednesday => "Tool.WorldClock.Weekday.Wed",
        DayOfWeek.Thursday => "Tool.WorldClock.Weekday.Thu",
        DayOfWeek.Friday => "Tool.WorldClock.Weekday.Fri",
        DayOfWeek.Saturday => "Tool.WorldClock.Weekday.Sat",
        _ => "Tool.WorldClock.Weekday.Sun",
    };

    private static string Key(int isoDay) => isoDay switch
    {
        1 => "Tool.WorldClock.Weekday.Mon",
        2 => "Tool.WorldClock.Weekday.Tue",
        3 => "Tool.WorldClock.Weekday.Wed",
        4 => "Tool.WorldClock.Weekday.Thu",
        5 => "Tool.WorldClock.Weekday.Fri",
        6 => "Tool.WorldClock.Weekday.Sat",
        _ => "Tool.WorldClock.Weekday.Sun",
    };
}
