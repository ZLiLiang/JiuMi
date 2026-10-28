using System;
using System.Collections.Generic;
using System.Linq;
using JiuMi.Application.WorldClock;

namespace JiuMi.Application.Tests;

/// <summary>
/// Validation of stored places and the bundled catalog documents.
/// </summary>
public sealed class WorldClockCatalogJsonTests
{
    private static readonly string[] _defaultPlaceIds =
        ["Asia/Shanghai", "Europe/London", "America/New_York", "America/Los_Angeles"];

    private const string CountriesJson = """
        {
          "countries": [
            { "iso2": "US", "name": { "zh": "美国", "en": "United States" }, "dialCode": "+1",
              "representative": { "zone": "America/New_York",
                "city": { "zh": "纽约", "en": "New York" } } },
            { "iso2": "JP", "name": { "zh": "日本" }, "dialCode": "+81",
              "representative": { "zone": "Asia/Tokyo" } }
          ]
        }
        """;

    private const string CitiesJson = """
        {
          "cities": [
            { "id": "America/New_York", "country": "US", "name": { "zh": "纽约", "en": "New York" } },
            { "id": "Asia/Tokyo", "country": "JP", "name": { "zh": "东京", "en": "Tokyo" } }
          ]
        }
        """;

    private const string OfficeHoursJson = """
        {
          "global": { "start": "09:00", "end": "18:00", "days": [1, 2, 3, 4, 5] },
          "overrides": [
            { "country": "AE", "start": "09:00", "end": "18:00", "days": [7, 1, 2, 3, 4],
              "source": "example", "updatedAt": "2026-10-28" }
          ]
        }
        """;

    [Fact]
    public void TryParse_ValidDocuments_BuildsTheCatalog()
    {
        bool parsed = WorldClockCatalogJson.TryParse(
            CountriesJson,
            CitiesJson,
            OfficeHoursJson,
            out WorldClockCatalog? catalog,
            out IReadOnlyList<string> issues);

        Assert.True(parsed);
        Assert.NotNull(catalog);
        Assert.Empty(issues);
        Assert.Equal(2, catalog!.Countries.Count);
        Assert.Equal(2, catalog.Cities.Count);

        bool presented = catalog.TryPresent(
            new Place(PlaceKind.Country, "US", WorkWindow.Default),
            "zh-CN",
            out PlacePresentation? presentation);

        Assert.True(presented);
        Assert.Equal("美国 · 纽约", presentation!.PrimaryName);
        Assert.Equal("+1", presentation.DialCode);
    }

    [Fact]
    public void TryParse_InvalidEntries_AreSkippedAndReported()
    {
        const string countries = """
            {
              "countries": [
                { "iso2": "US", "name": { "zh": "美国" }, "dialCode": "+1" },
                { "iso2": "JP", "name": { "zh": "日本" }, "dialCode": "+81",
                  "representative": { "zone": "Asia/Tokyo" } }
              ]
            }
            """;
        const string cities = """
            {
              "cities": [
                { "id": "Asia/Tokyo", "country": "ZZ", "name": { "zh": "东京" } }
              ]
            }
            """;
        const string officeHours = """
            {
              "global": { "start": "09:00", "end": "18:00", "days": [] },
              "overrides": []
            }
            """;

        bool parsed = WorldClockCatalogJson.TryParse(
            countries,
            cities,
            officeHours,
            out WorldClockCatalog? catalog,
            out IReadOnlyList<string> issues);

        Assert.True(parsed);
        Assert.NotNull(catalog);
        Assert.Single(catalog!.Countries);
        Assert.Empty(catalog.Cities);
        Assert.Equal(WorkWindow.Default, catalog.OfficeHours.Global);
        Assert.Equal(3, issues.Count);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse()
    {
        bool parsed = WorldClockCatalogJson.TryParse(
            "{ not json",
            CitiesJson,
            OfficeHoursJson,
            out WorldClockCatalog? catalog,
            out IReadOnlyList<string> issues);

        Assert.False(parsed);
        Assert.Null(catalog);
        Assert.Single(issues);
    }

    [Fact]
    public void OfficeHours_Resolve_PrefersCityThenCountryThenGlobal()
    {
        DateTime updated = new(2026, 10, 28);
        WorkWindow countryHours = new(new TimeOnly(8, 0), new TimeOnly(17, 0), [7, 1, 2, 3, 4]);
        WorkWindow cityHours = new(new TimeOnly(10, 0), new TimeOnly(19, 0), [1, 2, 3, 4, 5]);
        var catalog = new OfficeHoursCatalog(
            WorkWindow.Default,
            [
                new OfficeHoursOverride("AE", null, countryHours, "country", DateOnly.FromDateTime(updated)),
                new OfficeHoursOverride(null, "Asia/Dubai", cityHours, "city", DateOnly.FromDateTime(updated)),
            ]);

        Assert.Equal(cityHours, catalog.Resolve("AE", "Asia/Dubai"));
        Assert.Equal(countryHours, catalog.Resolve("AE", "Asia/Other"));
        Assert.Equal(WorkWindow.Default, catalog.Resolve("US", null));
    }

    [Fact]
    public void Place_TryCreate_RejectsEndBeforeStart()
    {
        bool created = Place.TryCreate(
            "city", "Asia/Shanghai", "18:00", "09:00", [1, 2, 3], out Place? place, out string? error);

        Assert.False(created);
        Assert.Null(place);
        Assert.NotNull(error);
    }

    [Fact]
    public void Place_TryCreate_RejectsOffGridMinutes()
    {
        bool created = Place.TryCreate(
            "city", "Asia/Shanghai", "09:10", "18:00", [1, 2, 3], out _, out string? error);

        Assert.False(created);
        Assert.NotNull(error);
    }

    [Fact]
    public void Place_TryCreate_RejectsAnEmptyWorkWeek()
    {
        bool created = Place.TryCreate(
            "city", "Asia/Shanghai", "09:00", "18:00", [], out _, out string? error);

        Assert.False(created);
        Assert.NotNull(error);
    }

    [Fact]
    public void Place_TryCreate_RejectsAnIanaCityIdWithoutAZone()
    {
        bool created = Place.TryCreate(
            "city", "Shanghai", "09:00", "18:00", [1], out _, out string? error);

        Assert.False(created);
        Assert.NotNull(error);
    }

    [Fact]
    public void Place_TryCreate_AcceptsCountryAndCityValues()
    {
        Assert.True(Place.TryCreate(
            "country", "us", "09:00", "18:00", [1, 2, 3, 4, 5], out Place? country, out _));
        Assert.Equal(PlaceKind.Country, country!.Kind);
        Assert.Equal("US", country.Id);
        Assert.Equal("country:US", country.Key);

        Assert.True(Place.TryCreate(
            "city", "Asia/Shanghai", "09:00", "18:00", [1, 2, 3, 4, 5], out Place? city, out _));
        Assert.Equal("city:Asia/Shanghai", city!.Key);
    }

    [Fact]
    public void WorldClockSettings_CreateDefault_UsesFourCitiesAndTheBoardView()
    {
        WorldClockSettings settings = WorldClockSettings.CreateDefault();

        Assert.Equal(WorldClockViewMode.Board, settings.View);
        Assert.Equal(4, settings.Places.Count);
        Assert.All(settings.Places, place => Assert.Equal(PlaceKind.City, place.Kind));
        Assert.Equal(
            _defaultPlaceIds,
            settings.Places.Select(place => place.Id).ToArray());
    }
}
