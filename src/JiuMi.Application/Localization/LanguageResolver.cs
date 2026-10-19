namespace JiuMi.Application.Localization;

/// <summary>
/// Resolves a requested language code against the languages in a manifest.
/// </summary>
public static class LanguageResolver
{
    /// <summary>
    /// Resolves <paramref name="requestedCode"/> by exact match, then language subtag,
    /// then the manifest source language.
    /// </summary>
    /// <param name="requestedCode">The requested code, such as <c>zh-Hans-CN</c>; may be null.</param>
    /// <param name="manifest">The languages available for display.</param>
    /// <returns>The code of the language to display, using the manifest's casing.</returns>
    /// <exception cref="ArgumentException">The manifest lists no languages.</exception>
    public static string Resolve(string? requestedCode, LanguageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Languages.Count == 0)
        {
            throw new ArgumentException("The language manifest lists no languages.", nameof(manifest));
        }

        if (string.IsNullOrWhiteSpace(requestedCode))
        {
            return manifest.Source;
        }

        string requested = requestedCode.Trim();
        foreach (LanguageOption language in manifest.Languages)
        {
            if (string.Equals(language.Code, requested, StringComparison.OrdinalIgnoreCase))
            {
                return language.Code;
            }
        }

        string requestedLanguage = GetLanguageSubtag(requested);
        foreach (LanguageOption language in manifest.Languages)
        {
            if (string.Equals(GetLanguageSubtag(language.Code), requestedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return language.Code;
            }
        }

        return manifest.Source;
    }

    private static string GetLanguageSubtag(string code)
    {
        int separator = code.IndexOfAny(['-', '_']);
        return separator < 0 ? code : code[..separator];
    }
}
