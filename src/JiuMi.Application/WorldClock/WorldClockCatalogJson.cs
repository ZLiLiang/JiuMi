using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Parses the bundled catalog files. Invalid entries are skipped and reported as diagnostics.
/// </summary>
public static class WorldClockCatalogJson
{
    /// <summary>
    /// Parses the three bundled catalog documents.
    /// </summary>
    /// <param name="countriesJson">The <c>countries.json</c> text.</param>
    /// <param name="citiesJson">The <c>cities.json</c> text.</param>
    /// <param name="officeHoursJson">The <c>office-hours.json</c> text.</param>
    /// <param name="catalog">The parsed catalog, when the documents are readable.</param>
    /// <param name="issues">Diagnostics for skipped entries and missing sections.</param>
    /// <returns><see langword="true"/> when the documents parse.</returns>
    public static bool TryParse(
        string countriesJson,
        string citiesJson,
        string officeHoursJson,
        out WorldClockCatalog? catalog,
        out IReadOnlyList<string> issues)
    {
        ArgumentNullException.ThrowIfNull(countriesJson);
        ArgumentNullException.ThrowIfNull(citiesJson);
        ArgumentNullException.ThrowIfNull(officeHoursJson);

        var problems = new List<string>();
        catalog = null;

        JsonDocument? countriesDocument = null;
        JsonDocument? citiesDocument = null;
        JsonDocument? officeHoursDocument = null;
        try
        {
            if (!TryReadDocument(countriesJson, "countries.json", problems, out countriesDocument) ||
                !TryReadDocument(citiesJson, "cities.json", problems, out citiesDocument) ||
                !TryReadDocument(officeHoursJson, "office-hours.json", problems, out officeHoursDocument))
            {
                issues = problems;
                return false;
            }

            IReadOnlyList<CountryCatalogEntry> countries =
                ReadCountries(countriesDocument!.RootElement, problems);
            IReadOnlyList<CityCatalogEntry> cities =
                ReadCities(citiesDocument!.RootElement, countries, problems);
            OfficeHoursCatalog officeHours =
                ReadOfficeHours(officeHoursDocument!.RootElement, problems);

            catalog = new WorldClockCatalog(countries, cities, officeHours);
        }
        finally
        {
            countriesDocument?.Dispose();
            citiesDocument?.Dispose();
            officeHoursDocument?.Dispose();
        }

        issues = problems;
        return true;
    }

    private static bool TryReadDocument(
        string json,
        string fileName,
        List<string> problems,
        out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException exception)
        {
            problems.Add($"{fileName}: {exception.Message}");
            document = null;
            return false;
        }
    }

    private static List<CountryCatalogEntry> ReadCountries(
        JsonElement root,
        List<string> problems)
    {
        var entries = new List<CountryCatalogEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetArray(root, "countries", problems, out JsonElement array))
        {
            return entries;
        }

        int index = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            index++;
            string? iso2 = ReadString(element, "iso2");
            string? dialCode = ReadString(element, "dialCode");
            string? zone = ReadString(element, "zone");
            if (iso2 is null || dialCode is null || zone is null ||
                !TryReadName(element, "name", out WorldClockName? name) ||
                name is null)
            {
                problems.Add($"countries[{index}]: missing iso2, name, dialCode or zone.");
                continue;
            }

            WorldClockName? representativeCity = null;
            if (element.TryGetProperty("representativeCity", out JsonElement representative) &&
                representative.ValueKind == JsonValueKind.Object &&
                !TryReadName(element, "representativeCity", out representativeCity))
            {
                problems.Add($"countries[{index}]: representativeCity has no usable name.");
            }

            try
            {
                var country = new CountryCatalogEntry(iso2, name, dialCode, zone, representativeCity);
                if (!seen.Add(country.Iso2))
                {
                    problems.Add($"countries[{index}]: duplicate iso2 {country.Iso2}.");
                    continue;
                }

                entries.Add(country);
            }
            catch (ArgumentException exception)
            {
                problems.Add($"countries[{index}]: {exception.Message}");
            }
        }

        return entries;
    }

    private static List<CityCatalogEntry> ReadCities(
        JsonElement root,
        IReadOnlyList<CountryCatalogEntry> countries,
        List<string> problems)
    {
        var entries = new List<CityCatalogEntry>();
        var countryCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CountryCatalogEntry country in countries)
        {
            countryCodes.Add(country.Iso2);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (!TryGetArray(root, "cities", problems, out JsonElement array))
        {
            return entries;
        }

        int index = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            index++;
            string? id = ReadString(element, "id");
            string? countryCode = ReadString(element, "country");
            if (id is null || countryCode is null || !TryReadName(element, "name", out WorldClockName? name) ||
                name is null)
            {
                problems.Add($"cities[{index}]: missing id, country or name.");
                continue;
            }

            if (!countryCodes.Contains(countryCode))
            {
                problems.Add($"cities[{index}]: unknown country {countryCode}.");
                continue;
            }

            try
            {
                var city = new CityCatalogEntry(id, countryCode, name);
                if (!seen.Add(city.Id))
                {
                    problems.Add($"cities[{index}]: duplicate id {city.Id}.");
                    continue;
                }

                entries.Add(city);
            }
            catch (ArgumentException exception)
            {
                problems.Add($"cities[{index}]: {exception.Message}");
            }
        }

        return entries;
    }

    private static OfficeHoursCatalog ReadOfficeHours(JsonElement root, List<string> problems)
    {
        WorkWindow global = WorkWindow.Default;
        if (root.TryGetProperty("global", out JsonElement globalElement) &&
            globalElement.ValueKind == JsonValueKind.Object)
        {
            if (!TryReadWindow(globalElement, out WorkWindow? parsedGlobal, out string? error) ||
                parsedGlobal is null)
            {
                problems.Add($"office-hours.global: {error}");
            }
            else
            {
                global = parsedGlobal;
            }
        }
        else
        {
            problems.Add("office-hours: missing global window.");
        }

        var overrides = new List<OfficeHoursOverride>();
        if (!root.TryGetProperty("overrides", out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            problems.Add("office-hours: missing overrides array.");
            return new OfficeHoursCatalog(global, overrides);
        }

        int index = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            index++;
            string? countryCode = ReadString(element, "country");
            string? cityId = ReadString(element, "city");
            string? source = ReadString(element, "source");
            string? updatedAt = ReadString(element, "updatedAt");

            if (!TryReadWindow(element, out WorkWindow? hours, out string? error) ||
                hours is null ||
                source is null ||
                updatedAt is null ||
                !DateOnly.TryParseExact(
                    updatedAt,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateOnly updatedDate))
            {
                problems.Add($"office-hours.overrides[{index}]: {error ?? "invalid source or updatedAt."}");
                continue;
            }

            try
            {
                overrides.Add(new OfficeHoursOverride(countryCode, cityId, hours, source, updatedDate));
            }
            catch (ArgumentException exception)
            {
                problems.Add($"office-hours.overrides[{index}]: {exception.Message}");
            }
        }

        return new OfficeHoursCatalog(global, overrides);
    }

    private static bool TryReadWindow(JsonElement element, out WorkWindow? window, out string? error)
    {
        string? start = ReadString(element, "start");
        string? end = ReadString(element, "end");
        if (!WorkWindow.TryParseTime(start, out TimeOnly startTime) ||
            !WorkWindow.TryParseTime(end, out TimeOnly endTime))
        {
            window = null;
            error = "start or end is not an HH:mm value.";
            return false;
        }

        if (!TryReadDays(element, out IReadOnlyList<int>? days))
        {
            window = null;
            error = "days must be an array of ISO weekdays.";
            return false;
        }

        return WorkWindow.TryCreate(startTime, endTime, days!, out window, out error);
    }

    private static bool TryReadDays(JsonElement element, out IReadOnlyList<int>? days)
    {
        days = null;
        if (!element.TryGetProperty("days", out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = new List<int>();
        foreach (JsonElement value in array.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int day))
            {
                return false;
            }

            values.Add(day);
        }

        days = values;
        return true;
    }

    private static bool TryReadName(JsonElement element, string property, out WorldClockName? name)
    {
        name = null;
        if (!element.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? zh = ReadString(value, "zh");
        string? en = ReadString(value, "en");
        if (string.IsNullOrWhiteSpace(zh))
        {
            return false;
        }

        name = new WorldClockName(zh, en);
        return true;
    }

    private static bool TryGetArray(
        JsonElement root,
        string property,
        List<string> problems,
        out JsonElement array)
    {
        array = default;
        if (!root.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            problems.Add($"missing array '{property}'.");
            return false;
        }

        array = value;
        return true;
    }

    private static string? ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }
}
