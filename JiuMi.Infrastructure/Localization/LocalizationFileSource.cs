using System.Text.Json;
using JiuMi.Application.Localization;

namespace JiuMi.Infrastructure.Localization;

/// <summary>
/// Loads localization data from a directory holding <c>languages.json</c> and one text file per language.
/// </summary>
public sealed class LocalizationFileSource : ILocalizationSource
{
    private const string ManifestFileName = "languages.json";
    private const int MinimumLanguageCodeLength = 2;
    private const int MaximumLanguageCodeLength = 35;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationFileSource"/> class.
    /// </summary>
    /// <param name="directory">The directory holding the manifest and the language files.</param>
    public LocalizationFileSource(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public LocalizationCatalog Load()
    {
        LanguageManifest manifest = ReadManifest();
        var languages = new List<LanguageOption>();
        var strings = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (LanguageOption language in manifest.Languages)
        {
            // A language whose file is missing or unreadable is skipped: the remaining languages
            // still work, and the source language carries the missing keys.
            IReadOnlyDictionary<string, string>? text = TryReadLanguage(language.Code);
            if (text is null)
            {
                continue;
            }

            languages.Add(language);
            strings[language.Code] = text;
        }

        if (!strings.ContainsKey(manifest.Source))
        {
            throw new InvalidDataException(
                $"The source language '{manifest.Source}' has no readable text file in '{_directory}'.");
        }

        return new LocalizationCatalog(new LanguageManifest(manifest.Source, languages), strings);
    }

    private LanguageManifest ReadManifest()
    {
        string path = Path.Combine(_directory, ManifestFileName);
        string json = ReadAllText(path, ManifestFileName);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("source", out JsonElement sourceElement) ||
                sourceElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"'{ManifestFileName}' does not declare a source language.");
            }

            string source = sourceElement.GetString()?.Trim() ?? string.Empty;
            if (!IsValidLanguageCode(source))
            {
                throw new InvalidDataException($"'{ManifestFileName}' declares an invalid source language.");
            }

            List<LanguageOption> languages = ReadLanguageOptions(root);
            if (languages.Count == 0)
            {
                throw new InvalidDataException($"'{ManifestFileName}' lists no usable languages.");
            }

            return new LanguageManifest(source, languages);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"'{ManifestFileName}' is not valid JSON.", exception);
        }
    }

    private static List<LanguageOption> ReadLanguageOptions(JsonElement root)
    {
        var languages = new List<LanguageOption>();
        if (!root.TryGetProperty("languages", out JsonElement entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return languages;
        }

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("code", out JsonElement codeElement) ||
                codeElement.ValueKind != JsonValueKind.String ||
                !entry.TryGetProperty("displayName", out JsonElement nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string code = codeElement.GetString()?.Trim() ?? string.Empty;
            string displayName = nameElement.GetString()?.Trim() ?? string.Empty;
            if (!IsValidLanguageCode(code) || displayName.Length == 0)
            {
                continue;
            }

            languages.Add(new LanguageOption(code, displayName));
        }

        return languages;
    }

    private Dictionary<string, string>? TryReadLanguage(string languageCode)
    {
        string path = Path.Combine(_directory, languageCode + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream, _serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ReadAllText(string path, string fileName)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The localization file '{fileName}' is missing.");
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException($"The localization file '{fileName}' cannot be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidDataException($"The localization file '{fileName}' cannot be read.", exception);
        }
    }

    private static bool IsValidLanguageCode(string code)
    {
        if (code.Length is < MinimumLanguageCodeLength or > MaximumLanguageCodeLength)
        {
            return false;
        }

        foreach (char character in code)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}
