using System;
using System.Collections.Generic;
using JiuMi.Application.WorldClock;

namespace JiuMi.Application.Tests;

/// <summary>
/// Shared catalog and zone helpers for the world clock tests.
/// </summary>
internal static class WorldClockTestData
{
    public static TimeZoneInfo Zone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    public static WorldClockCatalog Catalog()
    {
        WorkWindow defaultHours = WorkWindow.Default;
        CountryCatalogEntry[] countries =
        [
            new("US", new WorldClockName("美国", "United States"), "+1", "America/New_York", new WorldClockName("纽约", "New York")),
            new("JP", new WorldClockName("日本", "Japan"), "+81", "Asia/Tokyo", null),
            new("GB", new WorldClockName("英国", "United Kingdom"), "+44", "Europe/London", null),
            new("CN", new WorldClockName("中国", "China"), "+86", "Asia/Shanghai", null),
            new("NP", new WorldClockName("尼泊尔", "Nepal"), "+977", "Asia/Kathmandu", null),
            new("IN", new WorldClockName("印度", "India"), "+91", "Asia/Kolkata", null),
            new("AU", new WorldClockName("澳大利亚", "Australia"), "+61", "Australia/Sydney", new WorldClockName("悉尼", "Sydney")),
            new("ZZ", new WorldClockName("测试国", "Testland"), "+999", "America/New_York", null),
        ];
        CityCatalogEntry[] cities =
        [
            new("Asia/Shanghai", "CN", new WorldClockName("上海", "Shanghai")),
            new("Europe/London", "GB", new WorldClockName("伦敦", "London")),
            new("America/New_York", "US", new WorldClockName("纽约", "New York")),
            new("America/Los_Angeles", "US", new WorldClockName("旧金山", "San Francisco")),
            new("Asia/Tokyo", "JP", new WorldClockName("东京", "Tokyo")),
            new("Asia/Kathmandu", "NP", new WorldClockName("加德满都", "Kathmandu")),
            new("Asia/Kolkata", "IN", new WorldClockName("加尔各答", "Kolkata")),
            new("Australia/Sydney", "AU", new WorldClockName("悉尼", "Sydney")),
        ];
        return new WorldClockCatalog(countries, cities, new OfficeHoursCatalog(defaultHours, []));
    }

    public static Place City(string id, WorkWindow? hours = null) =>
        new(PlaceKind.City, id, hours ?? WorkWindow.Default);

    public static Place Country(string iso2, WorkWindow? hours = null) =>
        new(PlaceKind.Country, iso2, hours ?? WorkWindow.Default);

    public static WorkWindow Window(TimeOnly start, TimeOnly end, params int[] days) =>
        new(start, end, days.Length == 0 ? [1, 2, 3, 4, 5] : days);
}
