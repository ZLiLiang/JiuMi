namespace JiuMi.Application.Localization;

/// <summary>
/// A language the application can display, as listed in the language manifest.
/// </summary>
/// <param name="Code">The concrete language code, which also names the text file.</param>
/// <param name="DisplayName">The language name in its own language.</param>
public sealed record LanguageOption(string Code, string DisplayName);
