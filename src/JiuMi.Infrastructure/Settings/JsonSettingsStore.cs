using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiuMi.Application.FileRename;
using JiuMi.Application.ImageConversion;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.Settings;

/// <summary>
/// Reads and writes the half-stable JSON settings file with a temporary file and an atomic replace.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ISettingsLocationProvider _locationProvider;
    private SettingsLocation? _location;
    private SettingsLoadResult? _loadResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingsStore"/> class.
    /// </summary>
    /// <param name="locationProvider">Decides the settings location once, on first use.</param>
    public JsonSettingsStore(ISettingsLocationProvider locationProvider)
    {
        ArgumentNullException.ThrowIfNull(locationProvider);
        _locationProvider = locationProvider;
    }

    /// <inheritdoc />
    public SettingsLocation Location => _location ??= _locationProvider.Locate();

    /// <inheritdoc />
    public AppSettings Current { get; private set; } = AppSettings.CreateDefault();

    /// <inheritdoc />
    public SettingsLoadResult Load()
    {
        return _loadResult ??= LoadCore();
    }

    /// <inheritdoc />
    public SettingsSaveResult Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AppSettings normalized = settings with
        {
            SchemaVersion = SettingsContract.SchemaVersion,
        };
        Current = normalized;

        SettingsLocation location = Location;
        if (!location.IsPersistent)
        {
            return SettingsSaveResult.NotPersisted;
        }

        string settingsPath = location.FilePath!;
        string tempPath = Path.Combine(location.DirectoryPath!, SettingsContract.TempFileName);

        try
        {
            string json = JsonSerializer.Serialize(ToDocument(normalized), _serializerOptions);
            WriteTempFile(tempPath, json);
            File.Move(tempPath, settingsPath, overwrite: true);
            return SettingsSaveResult.Success;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            TryDelete(tempPath);
            return SettingsSaveResult.Failed(exception);
        }
    }

    private SettingsLoadResult LoadCore()
    {
        SettingsLocation location = Location;
        SettingsLoadIssue issues = GetLocationIssue(location);
        AppSettings settings = AppSettings.CreateDefault();
        Exception? error = null;

        if (!location.IsPersistent)
        {
            Current = settings;
            return new SettingsLoadResult(settings, location, issues);
        }

        TryDelete(Path.Combine(location.DirectoryPath!, SettingsContract.TempFileName));

        string settingsPath = location.FilePath!;
        if (!File.Exists(settingsPath))
        {
            Current = settings;
            return new SettingsLoadResult(settings, location, issues);
        }

        try
        {
            settings = ReadSettings(settingsPath, out bool invalidWorldClockPlaces);
            if (invalidWorldClockPlaces)
            {
                issues |= SettingsLoadIssue.InvalidWorldClockPlaces;
            }
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is JsonException)
        {
            issues |= SettingsLoadIssue.Corrupted;
            error = exception;
            settings = AppSettings.CreateDefault();
            BackupCorruptedFile(settingsPath, location.DirectoryPath!);
        }

        Current = settings;
        return new SettingsLoadResult(settings, location, issues, error);
    }

    private static SettingsLoadIssue GetLocationIssue(SettingsLocation location)
    {
        return location.Kind switch
        {
            SettingsLocationKind.AppData => SettingsLoadIssue.FallbackToAppData,
            SettingsLocationKind.MemoryOnly => SettingsLoadIssue.MemoryOnly,
            _ => SettingsLoadIssue.None,
        };
    }

    private static AppSettings ReadSettings(string settingsPath, out bool invalidWorldClockPlaces)
    {
        using FileStream stream = new(settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The settings file root is not a JSON object.");
        }

        WorldClockSettings worldClock = ReadWorldClock(root, out invalidWorldClockPlaces);
        return new AppSettings
        {
            SchemaVersion = ReadSchemaVersion(root),
            Language = ReadLanguage(root),
            Theme = ReadTheme(root),
            NavExpanded = ReadNavExpanded(root),
            OnlineDataEnabled = ReadOnlineDataEnabled(root),
            WorldClock = worldClock,
            ImageConvert = ReadImageConvert(root),
            FileRename = ReadFileRename(root),
        };
    }

    private static int ReadSchemaVersion(JsonElement root)
    {
        return root.TryGetProperty("schemaVersion", out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out int version)
            ? version
            : SettingsContract.SchemaVersion;
    }

    private static string? ReadLanguage(JsonElement root)
    {
        if (!root.TryGetProperty("language", out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? language = element.GetString()?.Trim();
        return string.IsNullOrEmpty(language) ? null : language;
    }

    private static AppTheme ReadTheme(JsonElement root)
    {
        if (!root.TryGetProperty("theme", out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return AppTheme.System;
        }

        return ParseTheme(element.GetString());
    }

    private static AppTheme ParseTheme(string? value)
    {
        if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
        {
            return AppTheme.Light;
        }

        if (string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return AppTheme.Dark;
        }

        return AppTheme.System;
    }

    private static bool ReadNavExpanded(JsonElement root)
    {
        return !root.TryGetProperty("navExpanded", out JsonElement element) ||
            element.ValueKind != JsonValueKind.False;
    }

    private static bool ReadOnlineDataEnabled(JsonElement root)
    {
        return !root.TryGetProperty("onlineDataEnabled", out JsonElement element) ||
            element.ValueKind != JsonValueKind.False;
    }

    private static ImageConvertSettings ReadImageConvert(JsonElement root)
    {
        if (!root.TryGetProperty("imageConvert", out JsonElement element) ||
            element.ValueKind != JsonValueKind.Object)
        {
            return ImageConvertSettings.CreateDefault();
        }

        string? outputDirectory = ReadString(element, "outputDirectory")?.Trim();
        if (string.IsNullOrEmpty(outputDirectory))
        {
            outputDirectory = null;
        }

        ImageRgbColor background = ImageRgbColor.White;
        if (ImageRgbColor.TryParse(ReadString(element, "jpegBackgroundColor"), out ImageRgbColor parsed))
        {
            background = parsed;
        }

        return new ImageConvertSettings
        {
            OutputDirectory = outputDirectory,
            JpegBackgroundColor = background,
        };
    }

    private static FileRenameSettings ReadFileRename(JsonElement root)
    {
        if (!root.TryGetProperty("fileRename", out JsonElement element) ||
            element.ValueKind != JsonValueKind.Object)
        {
            return FileRenameSettings.CreateDefault();
        }

        return new FileRenameSettings
        {
            RuleLevel = ParseRuleLevel(ReadString(element, "ruleLevel")),
            BasicTask = ParseBasicTask(ReadString(element, "basicTask")),
            BasicFind = ReadString(element, "basicFind") ?? string.Empty,
            BasicReplace = ReadString(element, "basicReplace") ?? string.Empty,
            AffixPosition = ParseAffixPosition(ReadString(element, "affixPosition")),
            AffixText = ReadString(element, "affixText") ?? string.Empty,
            SequenceName = ReadString(element, "sequenceName") ?? string.Empty,
            SequencePadWidth = ReadSequencePadWidth(element),
            AdvancedKind = ParseRuleKind(ReadString(element, "advancedKind")),
            AdvancedFind = ReadString(element, "advancedFind") ?? string.Empty,
            AdvancedReplace = ReadString(element, "advancedReplace") ?? string.Empty,
            AdvancedUseRegex = ReadBoolean(element, "advancedUseRegex"),
            Template = ReadString(element, "template") ?? string.Empty,
            IncludeSubdirectories = ReadBoolean(element, "includeSubdirectories"),
            Extensions = ReadString(element, "extensions") ?? string.Empty,
            BackupEnabled = ReadBoolean(element, "backupEnabled"),
        };
    }

    private static RenameRuleLevel ParseRuleLevel(string? value) =>
        string.Equals(value, "advanced", StringComparison.OrdinalIgnoreCase)
            ? RenameRuleLevel.Advanced
            : RenameRuleLevel.Basic;

    private static RenameBasicTask ParseBasicTask(string? value) => value?.ToUpperInvariant() switch
    {
        "AFFIX" => RenameBasicTask.Affix,
        "SEQUENCE" => RenameBasicTask.Sequence,
        _ => RenameBasicTask.Replace,
    };

    private static RenameAffixPosition ParseAffixPosition(string? value) =>
        string.Equals(value, "suffix", StringComparison.OrdinalIgnoreCase)
            ? RenameAffixPosition.Suffix
            : RenameAffixPosition.Prefix;

    private static RenameRuleKind ParseRuleKind(string? value) =>
        string.Equals(value, "template", StringComparison.OrdinalIgnoreCase)
            ? RenameRuleKind.Template
            : RenameRuleKind.Replace;

    private static int ReadSequencePadWidth(JsonElement element)
    {
        return element.TryGetProperty("sequencePadWidth", out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out int width) &&
            RenameRuleConverter.SequencePadWidths.Contains(width)
            ? width
            : 0;
    }

    private static bool ReadBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private static WorldClockSettings ReadWorldClock(JsonElement root, out bool invalidPlaces)
    {
        invalidPlaces = false;
        if (!root.TryGetProperty("worldClock", out JsonElement element) ||
            element.ValueKind != JsonValueKind.Object)
        {
            return WorldClockSettings.CreateDefault();
        }

        WorldClockViewMode view =
            string.Equals(ReadString(element, "view"), "plan", StringComparison.OrdinalIgnoreCase)
                ? WorldClockViewMode.Plan
                : WorldClockViewMode.Board;

        if (!element.TryGetProperty("places", out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            invalidPlaces = true;
            return WorldClockSettings.CreateDefault();
        }

        var places = new List<Place>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !Place.TryCreate(
                    ReadString(item, "kind"),
                    ReadString(item, "id"),
                    ReadString(item, "start"),
                    ReadString(item, "end"),
                    ReadDays(item),
                    out Place? place,
                    out _) ||
                place is null)
            {
                invalidPlaces = true;
                continue;
            }

            if (keys.Add(place.Key))
            {
                places.Add(place);
            }
        }

        return new WorldClockSettings(view, places);
    }

    private static List<int>? ReadDays(JsonElement element)
    {
        if (!element.TryGetProperty("days", out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var days = new List<int>();
        foreach (JsonElement value in array.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int day))
            {
                return null;
            }

            days.Add(day);
        }

        return days;
    }

    private static string? ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void WriteTempFile(string tempPath, string json)
    {
        using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using (var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true))
        {
            writer.Write(json);
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);
    }

    private static SettingsDocument ToDocument(AppSettings settings)
    {
        return new SettingsDocument
        {
            SchemaVersion = SettingsContract.SchemaVersion,
            Language = settings.Language,
            Theme = ToStorageValue(settings.Theme),
            NavExpanded = settings.NavExpanded,
            OnlineDataEnabled = settings.OnlineDataEnabled,
            ImageConvert = new ImageConvertDocument
            {
                OutputDirectory = settings.ImageConvert.OutputDirectory,
                JpegBackgroundColor = settings.ImageConvert.JpegBackgroundColor.ToHex(),
            },
            FileRename = new FileRenameDocument
            {
                RuleLevel = settings.FileRename.RuleLevel == RenameRuleLevel.Advanced ? "advanced" : "basic",
                BasicTask = ToStorageValue(settings.FileRename.BasicTask),
                BasicFind = settings.FileRename.BasicFind,
                BasicReplace = settings.FileRename.BasicReplace,
                AffixPosition = settings.FileRename.AffixPosition == RenameAffixPosition.Suffix ? "suffix" : "prefix",
                AffixText = settings.FileRename.AffixText,
                SequenceName = settings.FileRename.SequenceName,
                SequencePadWidth = settings.FileRename.SequencePadWidth,
                AdvancedKind = settings.FileRename.AdvancedKind == RenameRuleKind.Template ? "template" : "replace",
                AdvancedFind = settings.FileRename.AdvancedFind,
                AdvancedReplace = settings.FileRename.AdvancedReplace,
                AdvancedUseRegex = settings.FileRename.AdvancedUseRegex,
                Template = settings.FileRename.Template,
                IncludeSubdirectories = settings.FileRename.IncludeSubdirectories,
                Extensions = settings.FileRename.Extensions,
                BackupEnabled = settings.FileRename.BackupEnabled,
            },
            WorldClock = new WorldClockDocument
            {
                View = settings.WorldClock.View == WorldClockViewMode.Plan ? "plan" : "board",
                Places =
                [
                    .. settings.WorldClock.Places.Select(place => new PlaceDocument
                    {
                        Kind = place.Kind == PlaceKind.Country ? "country" : "city",
                        Id = place.Id,
                        Start = place.Hours.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                        End = place.Hours.End.ToString("HH:mm", CultureInfo.InvariantCulture),
                        Days = [.. place.Hours.Days],
                    }),
                ],
            },
        };
    }

    private static string ToStorageValue(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => "light",
            AppTheme.Dark => "dark",
            _ => "system",
        };
    }

    private static string ToStorageValue(RenameBasicTask task) => task switch
    {
        RenameBasicTask.Affix => "affix",
        RenameBasicTask.Sequence => "sequence",
        _ => "replace",
    };

    private static void BackupCorruptedFile(string settingsPath, string directoryPath)
    {
        try
        {
            File.Move(
                settingsPath,
                Path.Combine(directoryPath, SettingsContract.BackupFileName),
                overwrite: true);
        }
        catch (IOException)
        {
            // The startup path continues with defaults even when the corrupt file cannot be renamed.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsFileFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException;
    }

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

    private sealed class SettingsDocument
    {
        public int SchemaVersion
        {
            get;
            init;
        }

        public string? Language
        {
            get;
            init;
        }

        public string? Theme
        {
            get;
            init;
        }

        public bool NavExpanded
        {
            get;
            init;
        }

        public bool OnlineDataEnabled
        {
            get;
            init;
        }

        public ImageConvertDocument? ImageConvert
        {
            get;
            init;
        }

        public FileRenameDocument? FileRename
        {
            get;
            init;
        }

        public WorldClockDocument? WorldClock
        {
            get;
            init;
        }
    }

    private sealed class ImageConvertDocument
    {
        public string? OutputDirectory
        {
            get;
            init;
        }

        public string? JpegBackgroundColor
        {
            get;
            init;
        }
    }

    private sealed class FileRenameDocument
    {
        public string? RuleLevel
        {
            get;
            init;
        }

        public string? BasicTask
        {
            get;
            init;
        }

        public string? BasicFind
        {
            get;
            init;
        }

        public string? BasicReplace
        {
            get;
            init;
        }

        public string? AffixPosition
        {
            get;
            init;
        }

        public string? AffixText
        {
            get;
            init;
        }

        public string? SequenceName
        {
            get;
            init;
        }

        public int SequencePadWidth
        {
            get;
            init;
        }

        public string? AdvancedKind
        {
            get;
            init;
        }

        public string? AdvancedFind
        {
            get;
            init;
        }

        public string? AdvancedReplace
        {
            get;
            init;
        }

        public bool AdvancedUseRegex
        {
            get;
            init;
        }

        public string? Template
        {
            get;
            init;
        }

        public bool IncludeSubdirectories
        {
            get;
            init;
        }

        public string? Extensions
        {
            get;
            init;
        }

        public bool BackupEnabled
        {
            get;
            init;
        }
    }

    private sealed class WorldClockDocument
    {
        public string? View
        {
            get;
            init;
        }

        public List<PlaceDocument>? Places
        {
            get;
            init;
        }
    }

    private sealed class PlaceDocument
    {
        public string? Kind
        {
            get;
            init;
        }

        public string? Id
        {
            get;
            init;
        }

        public string? Start
        {
            get;
            init;
        }

        public string? End
        {
            get;
            init;
        }

        public List<int>? Days
        {
            get;
            init;
        }
    }
}
