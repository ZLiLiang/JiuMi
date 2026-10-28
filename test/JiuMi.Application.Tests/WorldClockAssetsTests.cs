using System;
using System.Collections.Generic;
using System.IO;
using JiuMi.Application.WorldClock;

namespace JiuMi.Application.Tests;

public sealed class WorldClockAssetsTests
{
    private static readonly string _directory = Path.Combine(AppContext.BaseDirectory, "Assets", "world-clock");

    [Fact]
    public void Catalog_GivenBundledFiles_ParsesWithoutIssues()
    {
        Assert.True(File.Exists(Path.Combine(_directory, "countries.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "cities.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "office-hours.json")));

        bool parsed = WorldClockCatalogJson.TryParse(
            File.ReadAllText(Path.Combine(_directory, "countries.json")),
            File.ReadAllText(Path.Combine(_directory, "cities.json")),
            File.ReadAllText(Path.Combine(_directory, "office-hours.json")),
            out WorldClockCatalog? catalog,
            out IReadOnlyList<string> issues);

        Assert.True(parsed);
        Assert.Empty(issues);
        Assert.NotNull(catalog);
        Assert.True(catalog!.Countries.Count >= 240, "The country catalog should cover ISO 3166.");
        Assert.InRange(catalog.Cities.Count, 100, 150);
    }

    [Fact]
    public void Countries_GivenBundledEntries_HaveResolvableZonesAndDialCodes()
    {
        WorldClockCatalog catalog = LoadCatalog();

        foreach (CountryCatalogEntry country in catalog.Countries)
        {
            Assert.True(country.DialCode.StartsWith('+'), $"{country.Iso2} has no dial code.");
            Assert.True(
                TimeZoneInfo.TryFindSystemTimeZoneById(country.Zone, out _),
                $"{country.Iso2} uses the unknown zone {country.Zone}.");
        }

        Assert.True(catalog.TryGetCountry("US", out CountryCatalogEntry? us));
        Assert.Equal("America/New_York", us!.Zone);
        Assert.NotNull(us.RepresentativeCity);
        Assert.Equal("+1", us.DialCode);

        Assert.True(catalog.TryGetCountry("JP", out CountryCatalogEntry? japan));
        Assert.Null(japan!.RepresentativeCity);

        // Germany and China each have one offset; tzdb links extra zones but the row must not
        // claim a representative city.
        Assert.True(catalog.TryGetCountry("DE", out CountryCatalogEntry? germany));
        Assert.Equal("Europe/Berlin", germany!.Zone);
        Assert.Null(germany.RepresentativeCity);
        Assert.True(catalog.TryGetCountry("CN", out CountryCatalogEntry? china));
        Assert.Equal("Asia/Shanghai", china!.Zone);
        Assert.Null(china.RepresentativeCity);

        Assert.True(catalog.TryGetCountry("AU", out CountryCatalogEntry? australia));
        Assert.NotNull(australia!.RepresentativeCity);

        Assert.True(catalog.TryGetCountry("HK", out CountryCatalogEntry? hongKong));
        Assert.Equal("+852", hongKong!.DialCode);
        Assert.Equal("香港", hongKong.Name.Zh);
    }

    [Fact]
    public void Cities_GivenBundledEntries_AreUniqueAndBelongToKnownCountries()
    {
        WorldClockCatalog catalog = LoadCatalog();
        var zones = new HashSet<string>(StringComparer.Ordinal);

        foreach (CityCatalogEntry city in catalog.Cities)
        {
            Assert.True(zones.Add(city.Id), $"The zone {city.Id} appears more than once.");
            Assert.False(string.IsNullOrWhiteSpace(city.Name.Zh), $"{city.Id} has no Chinese name.");
            Assert.False(string.IsNullOrWhiteSpace(city.Name.En), $"{city.Id} has no English name.");
            Assert.True(
                TimeZoneInfo.TryFindSystemTimeZoneById(city.Id, out _),
                $"{city.Id} is not a resolvable zone.");
            Assert.True(
                catalog.TryGetCountry(city.CountryCode, out _),
                $"{city.Id} points at the unknown country {city.CountryCode}.");
        }
    }

    private static WorldClockCatalog LoadCatalog()
    {
        bool parsed = WorldClockCatalogJson.TryParse(
            File.ReadAllText(Path.Combine(_directory, "countries.json")),
            File.ReadAllText(Path.Combine(_directory, "cities.json")),
            File.ReadAllText(Path.Combine(_directory, "office-hours.json")),
            out WorldClockCatalog? catalog,
            out _);

        Assert.True(parsed);
        Assert.NotNull(catalog);
        return catalog!;
    }
}
