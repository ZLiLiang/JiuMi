using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The bundled country, city and office-hours data, plus the lookups the page needs.
/// </summary>
public sealed class WorldClockCatalog
{
    private readonly IReadOnlyList<CountryCatalogEntry> _countries;
    private readonly IReadOnlyList<CityCatalogEntry> _cities;
    private readonly Dictionary<string, CountryCatalogEntry> _countryByCode;
    private readonly Dictionary<string, CityCatalogEntry> _cityById;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockCatalog"/> class.
    /// </summary>
    /// <param name="countries">The country entries.</param>
    /// <param name="cities">The city entries; ids are unique.</param>
    /// <param name="officeHours">The default office hours catalog.</param>
    public WorldClockCatalog(
        IReadOnlyList<CountryCatalogEntry> countries,
        IReadOnlyList<CityCatalogEntry> cities,
        OfficeHoursCatalog officeHours)
    {
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(officeHours);

        _countries = [.. countries];
        _cities = [.. cities];
        OfficeHours = officeHours;
        _countryByCode = new Dictionary<string, CountryCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        _cityById = new Dictionary<string, CityCatalogEntry>(StringComparer.Ordinal);

        foreach (CountryCatalogEntry country in _countries)
        {
            _countryByCode.TryAdd(country.Iso2, country);
        }

        foreach (CityCatalogEntry city in _cities)
        {
            _cityById.TryAdd(city.Id, city);
        }
    }

    /// <summary>
    /// Gets the country entries in catalog order.
    /// </summary>
    public IReadOnlyList<CountryCatalogEntry> Countries => _countries;

    /// <summary>
    /// Gets the city entries in catalog order.
    /// </summary>
    public IReadOnlyList<CityCatalogEntry> Cities => _cities;

    /// <summary>
    /// Gets the default office hours catalog.
    /// </summary>
    public OfficeHoursCatalog OfficeHours
    {
        get;
    }

    /// <summary>
    /// Tries to find a country entry.
    /// </summary>
    /// <param name="iso2">The ISO alpha-2 code.</param>
    /// <param name="country">The entry, when found.</param>
    /// <returns><see langword="true"/> when the catalog has the country.</returns>
    public bool TryGetCountry(string iso2, out CountryCatalogEntry? country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iso2);
        return _countryByCode.TryGetValue(iso2, out country);
    }

    /// <summary>
    /// Tries to find a city entry.
    /// </summary>
    /// <param name="id">The IANA time zone id.</param>
    /// <param name="city">The entry, when found.</param>
    /// <returns><see langword="true"/> when the catalog has the city.</returns>
    public bool TryGetCity(string id, out CityCatalogEntry? city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _cityById.TryGetValue(id, out city);
    }

    /// <summary>
    /// Gets the ISO alpha-2 code of the country a place belongs to.
    /// </summary>
    /// <param name="place">The place.</param>
    /// <returns>The country code, or <see langword="null"/> when the catalog cannot resolve it.</returns>
    public string? GetCountryCode(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.Kind == PlaceKind.Country)
        {
            return place.Id;
        }

        return _cityById.TryGetValue(place.Id, out CityCatalogEntry? city) ? city.CountryCode : null;
    }

    /// <summary>
    /// Resolves the default working window of a catalog entry.
    /// </summary>
    /// <param name="kind">The granularity of the entry.</param>
    /// <param name="id">The ISO alpha-2 code or IANA id.</param>
    /// <returns>The most specific window the office hours catalog provides.</returns>
    public WorkWindow GetDefaultHours(PlaceKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (kind == PlaceKind.Country)
        {
            return OfficeHours.Resolve(id, null);
        }

        string? countryCode = _cityById.TryGetValue(id, out CityCatalogEntry? city) ? city.CountryCode : null;
        return OfficeHours.Resolve(countryCode, id);
    }

    /// <summary>
    /// Builds the texts and data of one place.
    /// </summary>
    /// <param name="place">The place to present.</param>
    /// <param name="languageCode">The concrete interface language.</param>
    /// <param name="presentation">The presentation, when the catalog knows the place.</param>
    /// <returns><see langword="true"/> when the place exists in the catalog.</returns>
    public bool TryPresent(Place place, string languageCode, out PlacePresentation? presentation)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.Kind == PlaceKind.Country)
        {
            if (!_countryByCode.TryGetValue(place.Id, out CountryCatalogEntry? country))
            {
                presentation = null;
                return false;
            }

            WorldClockName? representative = country.RepresentativeCity;
            presentation = new PlacePresentation(
                place,
                Compose(country.Name.Resolve(languageCode), representative?.Resolve(languageCode)),
                Compose(country.Name.ResolveSecondary(languageCode), representative?.ResolveSecondary(languageCode)),
                country.DialCode,
                country.Zone,
                place.Hours);
            return true;
        }

        if (!_cityById.TryGetValue(place.Id, out CityCatalogEntry? city))
        {
            presentation = null;
            return false;
        }

        string dialCode = _countryByCode.TryGetValue(city.CountryCode, out CountryCatalogEntry? parent)
            ? parent.DialCode
            : string.Empty;
        presentation = new PlacePresentation(
            place,
            city.Name.Resolve(languageCode),
            city.Name.ResolveSecondary(languageCode),
            dialCode,
            city.Id,
            place.Hours);
        return true;
    }

    /// <summary>
    /// Searches the catalog for the manage-places list.
    /// </summary>
    /// <param name="kind">The segment being searched.</param>
    /// <param name="query">The search text; empty returns the whole segment.</param>
    /// <param name="languageCode">The concrete interface language.</param>
    /// <param name="culture">The culture that follows the interface language, used for sorting.</param>
    /// <returns>The matches in display order.</returns>
    public IReadOnlyList<CatalogItem> Search(
        PlaceKind kind,
        string? query,
        string languageCode,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        string trimmed = query?.Trim() ?? string.Empty;
        bool filtering = trimmed.Length > 0;
        StringComparer comparer = StringComparer.Create(culture, ignoreCase: true);

        if (kind == PlaceKind.Country)
        {
            IEnumerable<CatalogItem> countries = _countries
                .Where(country => !filtering || MatchesCountry(country, trimmed))
                .Select(country => new CatalogItem(
                    PlaceKind.Country,
                    country.Iso2,
                    country.Name.Resolve(languageCode),
                    country.Name.ResolveSecondary(languageCode),
                    country.DialCode,
                    groupName: null));
            return [.. countries.OrderBy(item => item.PrimaryName, comparer)];
        }

        IEnumerable<CatalogItem> cities = _cities
            .Where(city => !filtering || MatchesCity(city, trimmed))
            .Select(city =>
            {
                string? groupName = _countryByCode.TryGetValue(city.CountryCode, out CountryCatalogEntry? parent)
                    ? parent.Name.Resolve(languageCode)
                    : city.CountryCode;
                return new CatalogItem(
                    PlaceKind.City,
                    city.Id,
                    city.Name.Resolve(languageCode),
                    city.Name.ResolveSecondary(languageCode),
                    parent?.DialCode ?? string.Empty,
                    groupName);
            });

        return filtering
            ? [.. cities.OrderBy(item => item.PrimaryName, comparer)]
            : [.. cities.OrderBy(item => item.GroupName, comparer).ThenBy(item => item.PrimaryName, comparer)];
    }

    private static bool MatchesCountry(CountryCatalogEntry country, string query)
    {
        string dialDigits = country.DialCode.TrimStart('+');
        string haystack = string.Join(
            ' ',
            country.Name.Zh,
            country.Name.En,
            country.Iso2,
            country.DialCode,
            dialDigits,
            country.RepresentativeCity?.Zh,
            country.RepresentativeCity?.En);
        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesCity(CityCatalogEntry city, string query)
    {
        _countryByCode.TryGetValue(city.CountryCode, out CountryCatalogEntry? parent);
        string dialCode = parent?.DialCode ?? string.Empty;
        string haystack = string.Join(
            ' ',
            city.Name.Zh,
            city.Name.En,
            city.Id,
            parent?.Name.Zh,
            parent?.Name.En,
            dialCode,
            dialCode.TrimStart('+'));
        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string Compose(string name, string? suffix) =>
        string.IsNullOrEmpty(suffix) ? name : $"{name} · {suffix}";
}
