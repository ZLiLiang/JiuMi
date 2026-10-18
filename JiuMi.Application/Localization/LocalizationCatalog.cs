using System.Diagnostics.CodeAnalysis;

namespace JiuMi.Application.Localization;

/// <summary>
/// The loaded localization data: the available languages and their flat key-to-text dictionaries.
/// </summary>
public sealed class LocalizationCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _strings;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationCatalog"/> class.
    /// </summary>
    /// <param name="manifest">The languages that loaded successfully.</param>
    /// <param name="strings">The key-to-text dictionary of each loaded language.</param>
    public LocalizationCatalog(
        LanguageManifest manifest,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> strings)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(strings);

        Manifest = manifest;
        _strings = strings;
    }

    /// <summary>
    /// Gets the languages that loaded successfully.
    /// </summary>
    public LanguageManifest Manifest
    {
        get;
    }

    /// <summary>
    /// Gets the text dictionary of a language.
    /// </summary>
    /// <param name="languageCode">The language code to look up.</param>
    /// <param name="strings">The dictionary when the language loaded.</param>
    /// <returns><see langword="true"/> when the language is available.</returns>
    public bool TryGetStrings(
        string languageCode,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? strings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        return _strings.TryGetValue(languageCode, out strings);
    }
}
