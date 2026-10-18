using System.ComponentModel;
using System.Globalization;

namespace JiuMi.Application.Localization;

/// <summary>
/// Provides interface text for the current language and switches that language at runtime.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the text registered for <paramref name="key"/> in the current language.
    /// </summary>
    /// <param name="key">The localization key, such as <c>Home.Title</c>.</param>
    /// <returns>
    /// The text in the current language, else in the source language, else the key itself.
    /// Never throws for a missing key.
    /// </returns>
    string this[string key]
    {
        get;
    }

    /// <summary>
    /// Gets the text for <paramref name="key"/> with the arguments substituted.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="arguments">The values for the indexed placeholders.</param>
    /// <returns>The formatted text, using the culture of the current language.</returns>
    string Format(string key, params object?[] arguments);

    /// <summary>
    /// Gets the languages available for display, in manifest order.
    /// </summary>
    IReadOnlyList<LanguageOption> Languages
    {
        get;
    }

    /// <summary>
    /// Gets the concrete code of the language currently displayed.
    /// </summary>
    string CurrentLanguage
    {
        get;
    }

    /// <summary>
    /// Gets the culture that follows the displayed language.
    /// </summary>
    CultureInfo Culture
    {
        get;
    }

    /// <summary>
    /// Switches the displayed language and applies its culture.
    /// </summary>
    /// <param name="languageCode">The requested language code.</param>
    /// <returns>
    /// <see langword="true"/> when the displayed language is now the resolved one; <see langword="false"/>
    /// when the resolved language has no loaded text, in which case the current language is kept.
    /// </returns>
    bool TrySetLanguage(string languageCode);
}
