using System.Text.Json;

namespace JiuMi.Application.Tests;

public sealed class LocalizationAssetsTests
{
    private static readonly string _assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "i18n");
    private static readonly HashSet<string> _topLevelNamespaces = new(StringComparer.Ordinal)
    {
        "Shell",
        "Home",
        "Settings",
        "Tool",
        "Common",
        "Error",
    };

    [Fact]
    public void KeySets_AcrossLanguages_AreIdentical()
    {
        (string _, string[] codes) = ReadManifest();
        Dictionary<string, Dictionary<string, string>> catalogs = ReadCatalogs(codes);
        string[] expected = catalogs[codes[0]].Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        foreach (string code in codes)
        {
            string[] actual = catalogs[code].Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void PlaceholderSets_AcrossLanguages_AreIdenticalPerKey()
    {
        (string source, string[] codes) = ReadManifest();
        Dictionary<string, Dictionary<string, string>> catalogs = ReadCatalogs(codes);

        foreach (string key in catalogs[source].Keys)
        {
            int[] expected = ExtractPlaceholders(catalogs[source][key]);
            foreach (string code in codes)
            {
                int[] actual = ExtractPlaceholders(catalogs[code][key]);
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void Keys_GivenEveryLanguage_MatchNamingConventionAndTopLevelWhitelist()
    {
        (string _, string[] codes) = ReadManifest();
        Dictionary<string, Dictionary<string, string>> catalogs = ReadCatalogs(codes);

        foreach (string code in codes)
        {
            foreach (string key in catalogs[code].Keys)
            {
                Assert.True(IsValidKey(key), $"Language '{code}' has an invalid key '{key}'.");
                Assert.Contains(GetTopLevelNamespace(key), _topLevelNamespaces);
            }
        }
    }

    [Fact]
    public void Manifest_GivenEveryListedCode_HasParsableFileWithText()
    {
        (string source, string[] codes) = ReadManifest();

        Assert.Contains(source, codes);
        foreach (string code in codes)
        {
            string path = Path.Combine(_assetsDirectory, code + ".json");
            Assert.True(File.Exists(path), $"Language '{code}' has no text file.");

            Dictionary<string, string>? strings =
                JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            Assert.NotNull(strings);
            Assert.NotEmpty(strings);
        }
    }

    private static (string Source, string[] Codes) ReadManifest()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(_assetsDirectory, "languages.json")));
        JsonElement root = document.RootElement;
        string source = root.GetProperty("source").GetString() ?? string.Empty;
        string[] codes = root.GetProperty("languages")
            .EnumerateArray()
            .Select(language => language.GetProperty("code").GetString() ?? string.Empty)
            .ToArray();

        return (source, codes);
    }

    private static Dictionary<string, Dictionary<string, string>> ReadCatalogs(string[] codes) =>
        codes.ToDictionary(
            code => code,
            code => JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(_assetsDirectory, code + ".json"))) ?? []);

    private static int[] ExtractPlaceholders(string text)
    {
        var indices = new SortedSet<int>();
        for (int index = 0; index < text.Length - 2; index++)
        {
            if (text[index] != '{')
            {
                continue;
            }

            int end = text.IndexOf('}', index + 1);
            if (end < 0)
            {
                break;
            }

            if (int.TryParse(text.AsSpan(index + 1, end - index - 1), out int placeholder))
            {
                indices.Add(placeholder);
            }

            index = end;
        }

        return indices.ToArray();
    }

    private static bool IsValidKey(string key)
    {
        string[] segments = key.Split('.');
        if (segments.Length < 2)
        {
            return false;
        }

        foreach (string segment in segments)
        {
            if (segment.Length == 0 || !char.IsAsciiLetterUpper(segment[0]))
            {
                return false;
            }

            foreach (char character in segment)
            {
                if (!char.IsAsciiLetterOrDigit(character))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string GetTopLevelNamespace(string key)
    {
        int separator = key.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? key : key[..separator];
    }
}
