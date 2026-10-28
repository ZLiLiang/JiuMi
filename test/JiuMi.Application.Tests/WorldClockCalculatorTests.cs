using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JiuMi.Application.WorldClock;

namespace JiuMi.Application.Tests;

/// <summary>
/// Behaviour of the twenty-four hour grid, the status precedence and the overlap conclusion.
/// </summary>
public sealed class WorldClockCalculatorTests
{
    private static readonly string[] _cityGroupOrder =
        ["Australia", "China", "India", "Japan", "Nepal", "United Kingdom", "United States"];

    private static readonly WorldClockCatalog _catalog = WorldClockTestData.Catalog();

    [Fact]
    public void Build_LondonDstStart_SkipsTheMissingHour()
    {
        DateTimeOffset now = new(2026, 3, 29, 0, 30, 0, TimeSpan.Zero);
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Europe/London")],
            "zh-CN",
            now,
            TimeZoneInfo.Utc,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(0, row.GetLocalTime(timeline.Columns[0].Instant, HolidayCalendar.Empty).LocalDateTime.Hour);
        Assert.Equal(2, row.GetLocalTime(timeline.Columns[1].Instant, HolidayCalendar.Empty).LocalDateTime.Hour);
        Assert.Equal(3, row.GetLocalTime(timeline.Columns[2].Instant, HolidayCalendar.Empty).LocalDateTime.Hour);
    }

    [Fact]
    public void Build_NewYorkDstStart_SkipsTheMissingHour()
    {
        DateTimeOffset now = new(2026, 3, 8, 5, 0, 0, TimeSpan.Zero);
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("America/New_York")],
            "zh-CN",
            now,
            TimeZoneInfo.Utc,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(0, row.GetLocalTime(timeline.Columns[5].Instant, HolidayCalendar.Empty).LocalDateTime.Hour);
        Assert.Equal(1, row.GetLocalTime(timeline.Columns[6].Instant, HolidayCalendar.Empty).LocalDateTime.Hour);
        Assert.Equal(3, row.GetLocalTime(timeline.Columns[7].Instant, HolidayCalendar.Empty).LocalDateTime.Hour);
    }

    [Fact]
    public void Build_SydneyDstEnd_RepeatsTheHour()
    {
        DateTimeOffset now = new(2026, 4, 4, 0, 30, 0, TimeSpan.Zero);
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Australia/Sydney")],
            "zh-CN",
            now,
            TimeZoneInfo.Utc,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        DateTime first = row.GetLocalTime(timeline.Columns[15].Instant, HolidayCalendar.Empty).LocalDateTime;
        DateTime second = row.GetLocalTime(timeline.Columns[16].Instant, HolidayCalendar.Empty).LocalDateTime;
        Assert.Equal(2, first.Hour);
        Assert.Equal(2, second.Hour);
        Assert.Equal(1, (timeline.Columns[16].Instant - timeline.Columns[15].Instant).TotalHours);
    }

    [Fact]
    public void Build_Kathmandu_UsesTheFortyFiveMinuteOffset()
    {
        DateTimeOffset now = new(2026, 1, 1, 0, 30, 0, TimeSpan.Zero);
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Asia/Kathmandu")],
            "zh-CN",
            now,
            TimeZoneInfo.Utc,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(TimeOnly.Parse("08:45", CultureInfo.InvariantCulture), TimeOnly.FromDateTime(row.GetLocalTime(timeline.Columns[3].Instant, HolidayCalendar.Empty).LocalDateTime));
        Assert.Equal(WorldClockStatus.Off, row.Cells[3].Status);
        Assert.Equal(WorldClockStatus.Working, row.Cells[4].Status);
    }

    [Fact]
    public void Build_Kolkata_UsesTheHalfHourOffset()
    {
        DateTimeOffset now = new(2026, 1, 1, 0, 30, 0, TimeSpan.Zero);
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Asia/Kolkata")],
            "zh-CN",
            now,
            TimeZoneInfo.Utc,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(TimeOnly.Parse("08:30", CultureInfo.InvariantCulture), TimeOnly.FromDateTime(row.GetLocalTime(timeline.Columns[3].Instant, HolidayCalendar.Empty).LocalDateTime));
        Assert.Equal(WorldClockStatus.Off, row.Cells[3].Status);
        Assert.Equal(WorldClockStatus.Working, row.Cells[4].Status);
    }

    [Fact]
    public void Build_CrossingLocalMidnight_MarksTheDayBreakColumn()
    {
        DateTimeOffset now = new(2026, 1, 1, 0, 30, 0, TimeSpan.Zero);
        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Asia/Kathmandu")],
            "zh-CN",
            now,
            TimeZoneInfo.Utc,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.False(row.Cells[18].IsDayBreak);
        Assert.True(row.Cells[19].IsDayBreak);
        Assert.NotEqual(
            DateOnly.FromDateTime(timeline.Columns[18].Instant.UtcDateTime),
            DateOnly.FromDateTime(row.GetLocalTime(timeline.Columns[19].Instant, HolidayCalendar.Empty).LocalDateTime));
    }

    [Fact]
    public void Build_HolidayOnAWorkingDay_OverridesWorking()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 23, 12, 0, 0, TimeSpan.FromHours(-4));
        var holidays = new HolidayCalendar(new Dictionary<string, IReadOnlySet<DateOnly>>
        {
            ["US"] = new HashSet<DateOnly> { new(2026, 10, 23) },
        });

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("America/New_York")],
            "zh-CN",
            now,
            zone,
            holidays);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(WorldClockStatus.Holiday, row.Cells[12].Status);
        Assert.False(row.Cells[12].IsWorking);
        Assert.Equal(0, timeline.Overlap.MaxCount);
    }

    [Fact]
    public void Build_HolidayOnAWeekend_StaysHoliday()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(-4));
        var holidays = new HolidayCalendar(new Dictionary<string, IReadOnlySet<DateOnly>>
        {
            ["US"] = new HashSet<DateOnly> { new(2026, 10, 24) },
        });

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("America/New_York")],
            "zh-CN",
            now,
            zone,
            holidays);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(WorldClockStatus.Holiday, row.Cells[12].Status);
    }

    [Fact]
    public void Build_Weekend_OverridesOff()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(-4));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("America/New_York")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal(WorldClockStatus.Weekend, row.Cells[12].Status);
    }

    [Fact]
    public void Build_TwoPlacesInTheSameZone_ReportsFullOverlap()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-4));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("America/New_York"), WorldClockTestData.Country("US")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        Assert.True(timeline.Overlap.HasFullOverlap);
        OverlapRange range = Assert.Single(timeline.Overlap.Ranges);
        Assert.Equal(9, range.StartHour);
        Assert.Equal(18, range.EndHour);
    }

    [Fact]
    public void Build_TiedMaximums_ReturnEveryRange()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-4));
        WorkWindow morning = WorldClockTestData.Window(new TimeOnly(9, 0), new TimeOnly(12, 0));
        WorkWindow afternoon = WorldClockTestData.Window(new TimeOnly(15, 0), new TimeOnly(18, 0));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [
                WorldClockTestData.City("America/New_York"),
                WorldClockTestData.Country("US", morning),
                WorldClockTestData.Country("ZZ", afternoon),
            ],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        Assert.Equal(2, timeline.Overlap.MaxCount);
        Assert.Equal(2, timeline.Overlap.Ranges.Count);
        Assert.Equal(new OverlapRange(9, 12), timeline.Overlap.Ranges[0]);
        Assert.Equal(new OverlapRange(15, 18), timeline.Overlap.Ranges[1]);
        Assert.False(timeline.Overlap.HasFullOverlap);
    }

    [Fact]
    public void Build_OneWorkingPlace_ReportsFullOverlap()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("Asia/Shanghai");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(8));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Asia/Shanghai")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        Assert.True(timeline.Overlap.HasFullOverlap);
        OverlapRange range = Assert.Single(timeline.Overlap.Ranges);
        Assert.Equal(9, range.StartHour);
        Assert.Equal(18, range.EndHour);
    }

    [Fact]
    public void Build_UnknownCountry_DegradesInsteadOfUsingUtc()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-4));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.Country("XX")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.False(row.IsAvailable);
        Assert.Null(row.Zone);
        Assert.All(row.Cells, cell => Assert.Equal(WorldClockStatus.Unavailable, cell.Status));
        Assert.Equal(0, timeline.Overlap.PlaceCount);
    }

    [Fact]
    public void Build_WhenOnlyOnePlaceCanWork_ReportsNoOverlap()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-4));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("America/New_York"), WorldClockTestData.City("Asia/Tokyo")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        Assert.False(timeline.Overlap.HasOverlap);
        Assert.Empty(timeline.Overlap.Ranges);
        Assert.Equal(2, timeline.Overlap.PlaceCount);
    }

    [Fact]
    public void Build_BrokenZone_DegradesTheRowAndExcludesItFromOverlap()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("Asia/Shanghai");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(8));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Invalid/Zone"), WorldClockTestData.City("Asia/Shanghai")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        Assert.Equal(2, timeline.Rows.Count);
        Assert.False(timeline.Rows[0].IsAvailable);
        Assert.All(timeline.Rows[0].Cells, cell => Assert.Equal(WorldClockStatus.Unavailable, cell.Status));
        Assert.True(timeline.Rows[1].IsAvailable);
        Assert.Equal(1, timeline.Overlap.PlaceCount);
    }

    [Fact]
    public void Build_CountryPlace_UsesTheRepresentativeZoneAndCity()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("America/New_York");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-4));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.Country("US")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal("美国 · 纽约", row.Presentation.PrimaryName);
        Assert.Equal("United States · New York", row.Presentation.SecondaryName);
        Assert.Equal("+1", row.Presentation.DialCode);
        Assert.Equal("America/New_York", row.Presentation.ZoneId);
    }

    [Fact]
    public void Build_SingleZoneCountry_OmitsTheRepresentativeCity()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("Asia/Tokyo");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(9));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.Country("JP")],
            "en-US",
            now,
            zone,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal("Japan", row.Presentation.PrimaryName);
        Assert.Equal("日本", row.Presentation.SecondaryName);
    }

    [Fact]
    public void Build_UnknownPlace_StillShowsADegradedRow()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("Asia/Shanghai");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(8));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Unknown/Zone")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.Equal("Unknown/Zone", row.Presentation.PrimaryName);
        Assert.False(row.IsAvailable);
    }

    [Fact]
    public void Build_NightHours_AreMarkedOutsideTheWorkingWindow()
    {
        TimeZoneInfo zone = WorldClockTestData.Zone("Asia/Shanghai");
        DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(8));

        WorldClockTimeline timeline = WorldClockCalculator.Build(
            _catalog,
            [WorldClockTestData.City("Asia/Shanghai")],
            "zh-CN",
            now,
            zone,
            HolidayCalendar.Empty);

        WorldClockRow row = Assert.Single(timeline.Rows);
        Assert.True(row.Cells[5].IsNight);
        Assert.False(row.Cells[9].IsNight);
        Assert.True(row.Cells[22].IsNight);
        Assert.True(row.Cells[0].IsDayBreak);
    }

    [Fact]
    public void CatalogSearch_CityMode_MatchesDialCodeAndCountryName()
    {
        IReadOnlyList<CatalogItem> byDial = _catalog.Search(
            PlaceKind.City, "86", "zh-CN", CultureInfo.GetCultureInfo("zh-CN"));
        CatalogItem shanghai = Assert.Single(byDial);
        Assert.Equal("Asia/Shanghai", shanghai.Id);

        IReadOnlyList<CatalogItem> byCountry = _catalog.Search(
            PlaceKind.City, "China", "zh-CN", CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Contains(byCountry, item => item.Id == "Asia/Shanghai");
    }

    [Fact]
    public void CatalogSearch_CountryMode_MatchesRepresentativeCity()
    {
        IReadOnlyList<CatalogItem> matches = _catalog.Search(
            PlaceKind.Country, "New York", "zh-CN", CultureInfo.GetCultureInfo("zh-CN"));

        CatalogItem country = Assert.Single(matches);
        Assert.Equal("US", country.Id);
    }

    [Fact]
    public void CatalogSearch_CountryMode_MatchesAnyNanpCountryByDialCode()
    {
        IReadOnlyList<CatalogItem> matches = _catalog.Search(
            PlaceKind.Country, "+1", "zh-CN", CultureInfo.GetCultureInfo("zh-CN"));

        Assert.Contains(matches, item => item.Id == "US");
    }

    [Fact]
    public void CatalogSearch_WithoutQuery_GroupsCitiesByCountry()
    {
        IReadOnlyList<CatalogItem> items = _catalog.Search(
            PlaceKind.City, null, "en-US", CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Australia", items[0].GroupName);
        Assert.Equal(
            _cityGroupOrder,
            items.Select(item => item.GroupName).Distinct().ToArray());
    }

    [Fact]
    public void CatalogSearch_WithQuery_ReturnsFlatSortedList()
    {
        IReadOnlyList<CatalogItem> items = _catalog.Search(
            PlaceKind.City, "a", "en-US", CultureInfo.GetCultureInfo("en-US"));

        string[] names = [.. items.Select(item => item.PrimaryName)];
        Assert.Equal(names.OrderBy(name => name, StringComparer.Create(CultureInfo.GetCultureInfo("en-US"), ignoreCase: true)), names);
    }

    [Fact]
    public void TryPresent_MissingEnglishName_FallsBackToChinese()
    {
        var name = new WorldClockName("上海", null);

        Assert.Equal("上海", name.Resolve("en-US"));
        Assert.Equal("上海", name.ResolveSecondary("en-US"));
    }
}
