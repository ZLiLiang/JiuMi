namespace JiuMi.Application.Localization;

/// <summary>
/// Loads the localization data shipped with the application.
/// </summary>
public interface ILocalizationSource
{
    /// <summary>
    /// Loads the language manifest and the text file of every language it lists.
    /// </summary>
    /// <returns>
    /// The loaded catalog. A listed language whose file is missing or unreadable is skipped.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// The manifest, or the source language file, is missing or unreadable.
    /// </exception>
    LocalizationCatalog Load();
}
