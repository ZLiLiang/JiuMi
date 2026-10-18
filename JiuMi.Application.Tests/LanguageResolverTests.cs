using JiuMi.Application.Localization;

namespace JiuMi.Application.Tests;

public sealed class LanguageResolverTests
{
    private static readonly LanguageManifest _manifest = new(
        "zh-CN",
        [
            new LanguageOption("zh-CN", "中文（简体）"),
            new LanguageOption("en-US", "English"),
        ]);

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("zh-cn", "zh-CN")]
    [InlineData("EN-us", "en-US")]
    [InlineData("zh-Hans-CN", "zh-CN")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ja-JP", "zh-CN")]
    [InlineData("xx-YY", "zh-CN")]
    [InlineData("  en-US  ", "en-US")]
    public void Resolve_GivenRequestedCode_ReturnsResolvedLanguage(string requested, string expected)
    {
        string actual = LanguageResolver.Resolve(requested, _manifest);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_GivenNoRequestedCode_ReturnsSourceLanguage(string? requested)
    {
        string actual = LanguageResolver.Resolve(requested, _manifest);

        Assert.Equal("zh-CN", actual);
    }

    [Fact]
    public void Resolve_GivenManifestWithoutLanguages_ThrowsArgumentException()
    {
        var manifest = new LanguageManifest("zh-CN", []);

        Assert.Throws<ArgumentException>(() => LanguageResolver.Resolve("en-US", manifest));
    }
}
