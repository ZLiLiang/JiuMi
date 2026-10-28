using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Fetches public holidays from Nager.Date, keeping a seven-day cache beside the settings.
/// </summary>
public sealed class NagerDateHolidayProvider : IHolidayProvider, IDisposable
{
    private static readonly Uri _baseAddress = new("https://date.nager.at/");
    private static readonly IReadOnlySet<DateOnly> _emptyDates = new HashSet<DateOnly>();

    private readonly HttpClient _client;
    private readonly HolidayCache _cache;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NagerDateHolidayProvider"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies the directory the cache lives under.</param>
    /// <param name="timeProvider">Supplies the current time for cache freshness.</param>
    /// <param name="logger">Receives cache and request failures.</param>
    public NagerDateHolidayProvider(
        ISettingsStore settingsStore,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
        : this(settingsStore, timeProvider, logger, handler: null)
    {
    }

    internal NagerDateHolidayProvider(
        ISettingsStore settingsStore,
        TimeProvider timeProvider,
        Serilog.ILogger logger,
        HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _cache = new HolidayCache(settingsStore.Location.DirectoryPath, timeProvider, logger);
        _client = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);
        _client.BaseAddress = _baseAddress;
        _client.Timeout = TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc />
    public async Task<HolidayLookup> GetHolidaysAsync(
        string countryCode,
        int year,
        bool allowNetwork,
        CancellationToken cancellationToken = default)
    {
        Validate(countryCode, year);

        if (!allowNetwork)
        {
            return _cache.TryRead(countryCode, year, allowExpired: true, out HolidayLookup? cached) &&
                cached is not null
                ? cached
                : new HolidayLookup(HolidayLookupStatus.Disabled, _emptyDates);
        }

        if (_cache.TryRead(countryCode, year, allowExpired: false, out HolidayLookup? fresh) &&
            fresh is not null)
        {
            return fresh;
        }

        HolidayLookup fetched = await FetchAsync(countryCode, year, cancellationToken).ConfigureAwait(false);
        switch (fetched.Status)
        {
            case HolidayLookupStatus.Available:
                _cache.Write(countryCode, year, fetched.Dates);
                return fetched;
            case HolidayLookupStatus.Failed:
                return _cache.TryRead(countryCode, year, allowExpired: true, out HolidayLookup? stale) &&
                    stale is not null
                    ? stale
                    : fetched;
            default:
                return fetched;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private async Task<HolidayLookup> FetchAsync(
        string countryCode,
        int year,
        CancellationToken cancellationToken)
    {
        string requestUri = $"api/v3/PublicHolidays/{year.ToString(CultureInfo.InvariantCulture)}/{countryCode}";
        Exception? failure = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await _client.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return HolidayLookup.NoData;
                }

                if (!response.IsSuccessStatusCode)
                {
                    failure = new HttpRequestException(
                        $"The holiday service answered {(int)response.StatusCode}.");
                    continue;
                }

                string body = await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                HashSet<DateOnly>? dates = ReadDates(body);
                if (dates is null)
                {
                    return new HolidayLookup(
                        HolidayLookupStatus.Failed,
                        _emptyDates,
                        new InvalidDataException("The holiday service returned an unreadable response."));
                }

                return dates.Count == 0
                    ? HolidayLookup.NoData
                    : new HolidayLookup(HolidayLookupStatus.Available, dates);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failure = new TimeoutException("The holiday request timed out.");
            }
            catch (HttpRequestException exception)
            {
                failure = exception;
            }
            catch (IOException exception)
            {
                failure = exception;
            }
        }

        _logger.Warning(failure, "Holiday request for {CountryCode} {Year} failed", countryCode, year);
        return new HolidayLookup(HolidayLookupStatus.Failed, _emptyDates, failure);
    }

    private static HashSet<DateOnly>? ReadDates(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var dates = new HashSet<DateOnly>();
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("date", out JsonElement dateElement) &&
                    dateElement.ValueKind == JsonValueKind.String &&
                    DateOnly.TryParseExact(
                        dateElement.GetString(),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateOnly date))
                {
                    dates.Add(date);
                }
            }

            return dates;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Validate(string countryCode, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        if (countryCode.Length != 2 ||
            !char.IsAsciiLetter(countryCode[0]) ||
            !char.IsAsciiLetter(countryCode[1]))
        {
            throw new ArgumentException("A country code must be an ISO alpha-2 code.", nameof(countryCode));
        }

        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }
    }
}
