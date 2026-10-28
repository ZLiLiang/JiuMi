using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// A bilingual name from the bundled catalog.
/// </summary>
public sealed record WorldClockName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockName"/> class.
    /// </summary>
    /// <param name="zh">The Chinese name.</param>
    /// <param name="en">The English name; falls back to <paramref name="zh"/> when missing.</param>
    public WorldClockName(string zh, string? en)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zh);
        Zh = zh;
        En = string.IsNullOrWhiteSpace(en) ? zh : en;
    }

    /// <summary>
    /// Gets the Chinese name.
    /// </summary>
    public string Zh
    {
        get;
    }

    /// <summary>
    /// Gets the English name, or the Chinese name when the catalog has no English text.
    /// </summary>
    public string En
    {
        get;
    }

    /// <summary>
    /// Resolves the name in the interface language.
    /// </summary>
    /// <param name="languageCode">The concrete interface language, such as <c>zh-CN</c>.</param>
    /// <returns>The English name for English, otherwise Chinese.</returns>
    public string Resolve(string languageCode) => IsEnglish(languageCode) ? En : Zh;

    /// <summary>
    /// Resolves the name in the language that is not the primary one.
    /// </summary>
    /// <param name="languageCode">The concrete interface language.</param>
    /// <returns>The other language, which is what the secondary line shows.</returns>
    public string ResolveSecondary(string languageCode) => IsEnglish(languageCode) ? Zh : En;

    private static bool IsEnglish(string languageCode) =>
        !string.IsNullOrEmpty(languageCode) &&
        languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
