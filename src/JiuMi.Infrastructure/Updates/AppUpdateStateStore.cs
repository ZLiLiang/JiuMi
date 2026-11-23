using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiuMi.Application.Updates;

namespace JiuMi.Infrastructure.Updates;

/// <summary>
/// Persists <see cref="AppUpdateState"/> to <c>updates/state.json</c> beside the settings file.
/// </summary>
internal sealed class AppUpdateStateStore
{
    private const string StateFileName = "state.json";

    private static readonly JsonSerializerOptions _documentOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly string? _directory;
    private readonly Serilog.ILogger _logger;
    private readonly object _sync = new();
    private AppUpdateState _memoryState = new(null, null);

    public AppUpdateStateStore(string? settingsDirectory, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _directory = settingsDirectory is null
            ? null
            : Path.Combine(settingsDirectory, "updates");
        _logger = logger;
    }

    public AppUpdateState Read()
    {
        lock (_sync)
        {
            if (_directory is null)
            {
                return _memoryState;
            }

            try
            {
                string path = Path.Combine(_directory, StateFileName);
                if (!File.Exists(path))
                {
                    return new AppUpdateState(null, null);
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                DateTimeOffset? lastCheckedAt = null;
                if (document.RootElement.TryGetProperty("lastCheckedAt", out JsonElement checkedElement) &&
                    checkedElement.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(
                        checkedElement.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out DateTimeOffset parsedCheckedAt))
                {
                    lastCheckedAt = parsedCheckedAt;
                }

                string? lastNotifiedVersion = null;
                if (document.RootElement.TryGetProperty("lastNotifiedVersion", out JsonElement notifiedElement) &&
                    notifiedElement.ValueKind == JsonValueKind.String)
                {
                    string? raw = notifiedElement.GetString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        lastNotifiedVersion = raw.Trim();
                    }
                }

                return new AppUpdateState(lastCheckedAt, lastNotifiedVersion);
            }
            catch (Exception exception) when (IsFileFailure(exception) || exception is JsonException)
            {
                _logger.Debug(exception, "App update state could not be read");
                return new AppUpdateState(null, null);
            }
        }
    }

    public void Write(AppUpdateState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            _memoryState = state;
            if (_directory is null)
            {
                return;
            }

            string path = Path.Combine(_directory, StateFileName);
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(
                    temporary,
                    JsonSerializer.Serialize(
                        new StateDocument
                        {
                            LastCheckedAt = state.LastCheckedAt?.ToString("O", CultureInfo.InvariantCulture),
                            LastNotifiedVersion = state.LastNotifiedVersion,
                        },
                        _documentOptions));
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception exception) when (IsFileFailure(exception))
            {
                _logger.Debug(exception, "App update state could not be written");
                TryDeleteFile(temporary);
            }
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Temporary app update state file could not be removed");
        }
    }

    private static bool IsFileFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException;

    private sealed class StateDocument
    {
        public string? LastCheckedAt
        {
            get;
            init;
        }

        public string? LastNotifiedVersion
        {
            get;
            init;
        }
    }
}
