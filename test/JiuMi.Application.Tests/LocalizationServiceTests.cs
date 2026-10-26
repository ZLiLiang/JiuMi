using System.Globalization;
using JiuMi.Application.Localization;

namespace JiuMi.Application.Tests;

public sealed class LocalizationServiceTests
{
    private static readonly Serilog.ILogger _logger = Serilog.Core.Logger.None;

    private static readonly LanguageManifest _manifest = new(
        "zh-CN",
        [
            new LanguageOption("zh-CN", "中文（简体）"),
            new LanguageOption("en-US", "English"),
        ]);

    [Theory]
    [InlineData("zh-Hans-CN", "zh-CN")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ja-JP", "zh-CN")]
    public void Constructor_GivenRequestedLanguage_ResolvesLanguageAndCulture(string requested, string expected)
    {
        using var culture = new CultureScope();

        var service = new LocalizationService(CreateCatalog(), requested, _logger);

        Assert.Equal(expected, service.CurrentLanguage);
        Assert.Equal(expected, service.Culture.Name);
        Assert.Equal(expected, CultureInfo.CurrentCulture.Name);
        Assert.Equal(expected, CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void Indexer_GivenKeyMissingInCurrentLanguage_FallsBackToSourceLanguage()
    {
        using var culture = new CultureScope();
        var service = new LocalizationService(CreateCatalog(), "en-US", _logger);

        Assert.Equal("仅中文", service["Test.SourceOnly"]);
    }

    [Fact]
    public void Indexer_GivenKeyMissingInEveryLanguage_ReturnsKeyName()
    {
        using var culture = new CultureScope();
        var service = new LocalizationService(CreateCatalog(), "en-US", _logger);

        Assert.Equal("Test.Missing", service["Test.Missing"]);
    }

    [Fact]
    public void Format_GivenPlaceholders_SubstitutesArgumentsInOrder()
    {
        using var culture = new CultureScope();
        var service = new LocalizationService(CreateCatalog(), "en-US", _logger);

        Assert.Equal("Hello, Ada! (2)", service.Format("Test.Placeholder", "Ada", 2));
    }

    [Fact]
    public void TrySetLanguage_GivenLoadedLanguage_UpdatesTextCultureAndNotifies()
    {
        using var culture = new CultureScope();
        var service = new LocalizationService(CreateCatalog(), "zh-CN", _logger);
        var changedProperties = new List<string?>();
        service.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        bool switched = service.TrySetLanguage("en-US");

        Assert.True(switched);
        Assert.Equal("en-US", service.CurrentLanguage);
        Assert.Equal("Hello", service["Test.Hello"]);
        Assert.Equal("en-US", service.Culture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
        Assert.Contains("Item[]", changedProperties);
        Assert.Contains(nameof(ILocalizationService.CurrentLanguage), changedProperties);
        Assert.Contains(nameof(ILocalizationService.Culture), changedProperties);
    }

    [Fact]
    public void TrySetLanguage_GivenLanguageWithoutLoadedText_KeepsCurrentLanguage()
    {
        using var culture = new CultureScope();
        var manifest = new LanguageManifest(
            "zh-CN",
            [
                new LanguageOption("zh-CN", "中文（简体）"),
                new LanguageOption("en-US", "English"),
                new LanguageOption("fr-FR", "Français"),
            ]);
        var strings = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Test.Hello"] = "你好",
            },
        };
        var service = new LocalizationService(
            new LocalizationCatalog(manifest, strings),
            "zh-CN",
            _logger);

        bool switched = service.TrySetLanguage("fr-FR");

        Assert.False(switched);
        Assert.Equal("zh-CN", service.CurrentLanguage);
        Assert.Equal("你好", service["Test.Hello"]);
    }

    [Fact]
    public void Culture_GivenEnglishLanguage_FormatsDatesInEnglish()
    {
        using var culture = new CultureScope();
        var service = new LocalizationService(CreateCatalog(), "en-US", _logger);
        var date = new DateTime(2026, 10, 18);

        Assert.Equal("10/18/2026", date.ToString("d", service.Culture));
    }

    private static LocalizationCatalog CreateCatalog() =>
        new(
            _manifest,
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Test.Hello"] = "你好",
                    ["Test.SourceOnly"] = "仅中文",
                    ["Test.Placeholder"] = "你好，{0}！（{1}）",
                },
                ["en-US"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Test.Hello"] = "Hello",
                    ["Test.Placeholder"] = "Hello, {0}! ({1})",
                },
            });
}
