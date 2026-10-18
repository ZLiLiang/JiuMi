namespace JiuMi.Application.Localization;

/// <summary>
/// The languages available for display and the language used when a requested one is unavailable.
/// </summary>
public sealed class LanguageManifest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageManifest"/> class.
    /// </summary>
    /// <param name="source">The fallback language code.</param>
    /// <param name="languages">The languages available for display, in listing order.</param>
    public LanguageManifest(string source, IReadOnlyList<LanguageOption> languages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(languages);

        Source = source;
        Languages = languages;
    }

    /// <summary>
    /// Gets the fallback language code, which is also the end of the resolution chain.
    /// </summary>
    public string Source
    {
        get;
    }

    /// <summary>
    /// Gets the languages available for display, in listing order.
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages
    {
        get;
    }
}
