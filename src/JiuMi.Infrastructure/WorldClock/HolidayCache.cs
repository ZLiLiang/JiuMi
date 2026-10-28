using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Stores holiday lookups beside the settings file and reads them back within their lifetime.
/// </summary>
internal sealed class HolidayCache
{
    private static readonly TimeSpan _timeToLive = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string? _directory;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;

    public HolidayCache(string? settingsDirectory, TimeProvider timeProvider, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _directory = settingsDirectory is null
            ? null
            : Path.Combine(settingsDirectory, "cache", "holidays");
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool TryRead(
        string countryCode,
        int year,
        bool allowExpired,
        out HolidayLookup? lookup)
    {
        lookup = null;
        if (_directory is null)
        {
            return false;
        }

        string path = GetPath(countryCode, year);
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("fetchedAt", out JsonElement fetchedAtElement) ||
                fetchedAtElement.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParseExact(
                    fetchedAtElement.GetString(),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset fetchedAt) ||
                !root.TryGetProperty("dates", out JsonElement datesElement) ||
                datesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (!allowExpired && _timeProvider.GetUtcNow() - fetchedAt > _timeToLive)
            {
                return false;
            }

            var dates = new HashSet<DateOnly>();
            foreach (JsonElement dateElement in datesElement.EnumerateArray())
            {
                if (dateElement.ValueKind == JsonValueKind.String &&
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

            lookup = new HolidayLookup(HolidayLookupStatus.Available, dates);
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is JsonException)
        {
            _logger.Debug(exception, "Holiday cache entry could not be read");
            return false;
        }
    }

    public void Write(string countryCode, int year, IReadOnlySet<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        if (_directory is null)
        {
            return;
        }

        string path = GetPath(countryCode, year);
        string tempPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            var document = new CacheDocument
            {
                FetchedAt = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                Dates =
                [
                    .. dates
                        .OrderBy(date => date)
                        .Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ],
            };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(document, _serializerOptions));
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Holiday cache entry could not be written");
            TryDelete(tempPath);
        }
    }

    private string GetPath(string countryCode, int year) =>
        Path.Combine(_directory!, $"{countryCode.ToUpperInvariant()}-{year}.json");

    private static bool IsFileFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class CacheDocument
    {
        public string? FetchedAt
        {
            get;
            init;
        }

        public List<string>? Dates
        {
            get;
            init;
        }
    }
}
