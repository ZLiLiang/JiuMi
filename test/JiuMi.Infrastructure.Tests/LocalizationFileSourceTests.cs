using JiuMi.Application.Localization;
using JiuMi.Infrastructure.Localization;

namespace JiuMi.Infrastructure.Tests;

public sealed class LocalizationFileSourceTests : IDisposable
{
    private const string ManifestJson = """
        {
          "source": "zh-CN",
          "languages": [
            { "code": "zh-CN", "displayName": "中文（简体）" },
            { "code": "en-US", "displayName": "English" }
          ]
        }
        """;

    private static readonly string[] _chineseOnly = ["zh-CN"];
    private static readonly string[] _chineseAndEnglish = ["zh-CN", "en-US"];

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));

    public LocalizationFileSourceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_GivenValidFiles_ReturnsManifestOrderAndText()
    {
        WriteManifest(ManifestJson);
        WriteLanguage("zh-CN", """{"Test.Hello":"你好"}""");
        WriteLanguage("en-US", """{"Test.Hello":"Hello"}""");

        LocalizationCatalog catalog = new LocalizationFileSource(_directory).Load();

        Assert.Equal("zh-CN", catalog.Manifest.Source);
        Assert.Equal(_chineseAndEnglish, catalog.Manifest.Languages.Select(language => language.Code));
        Assert.True(catalog.TryGetStrings("en-US", out IReadOnlyDictionary<string, string>? strings));
        Assert.Equal("Hello", strings["Test.Hello"]);
    }

    [Fact]
    public void Load_GivenMissingLanguageFile_SkipsThatLanguage()
    {
        WriteManifest(ManifestJson);
        WriteLanguage("zh-CN", """{"Test.Hello":"你好"}""");

        LocalizationCatalog catalog = new LocalizationFileSource(_directory).Load();

        Assert.Equal(_chineseOnly, catalog.Manifest.Languages.Select(language => language.Code));
        Assert.False(catalog.TryGetStrings("en-US", out _));
    }

    [Fact]
    public void Load_GivenUnparsableLanguageFile_SkipsThatLanguage()
    {
        WriteManifest(ManifestJson);
        WriteLanguage("zh-CN", """{"Test.Hello":"你好"}""");
        WriteLanguage("en-US", "{ this is not json");

        LocalizationCatalog catalog = new LocalizationFileSource(_directory).Load();

        Assert.Equal(_chineseOnly, catalog.Manifest.Languages.Select(language => language.Code));
    }

    [Fact]
    public void Load_GivenMissingManifest_ThrowsInvalidDataException()
    {
        var source = new LocalizationFileSource(_directory);

        Assert.Throws<InvalidDataException>(source.Load);
    }

    [Fact]
    public void Load_GivenMissingSourceLanguageFile_ThrowsInvalidDataException()
    {
        WriteManifest(ManifestJson);
        WriteLanguage("en-US", """{"Test.Hello":"Hello"}""");
        var source = new LocalizationFileSource(_directory);

        Assert.Throws<InvalidDataException>(source.Load);
    }

    [Fact]
    public void Load_GivenLanguageCodeWithPathSeparator_SkipsThatLanguage()
    {
        WriteManifest(
            """
            {
              "source": "zh-CN",
              "languages": [
                { "code": "zh-CN", "displayName": "中文（简体）" },
                { "code": "../en-US", "displayName": "English" }
              ]
            }
            """);
        WriteLanguage("zh-CN", """{"Test.Hello":"你好"}""");

        LocalizationCatalog catalog = new LocalizationFileSource(_directory).Load();

        Assert.Equal(_chineseOnly, catalog.Manifest.Languages.Select(language => language.Code));
    }

    [Fact]
    public void Load_GivenShippedAssets_ReturnsChineseAndEnglish()
    {
        var source = new LocalizationFileSource(Path.Combine(AppContext.BaseDirectory, "Assets", "i18n"));

        LocalizationCatalog catalog = source.Load();

        Assert.Equal("zh-CN", catalog.Manifest.Source);
        Assert.Equal(_chineseAndEnglish, catalog.Manifest.Languages.Select(language => language.Code));
        Assert.True(catalog.TryGetStrings("zh-CN", out IReadOnlyDictionary<string, string>? strings));
        Assert.False(string.IsNullOrWhiteSpace(strings["Home.Title"]));
    }

    private void WriteManifest(string json) =>
        File.WriteAllText(Path.Combine(_directory, "languages.json"), json);

    private void WriteLanguage(string code, string json) =>
        File.WriteAllText(Path.Combine(_directory, code + ".json"), json);
}
