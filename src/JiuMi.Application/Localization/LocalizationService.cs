using System.ComponentModel;
using System.Globalization;

namespace JiuMi.Application.Localization;

/// <summary>
/// Serves interface text from a loaded catalog and keeps the current culture in sync with it.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly LocalizationCatalog _catalog;
    private readonly Serilog.ILogger _logger;
    private string _currentLanguage;
    private CultureInfo _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationService"/> class.
    /// </summary>
    /// <param name="catalog">The loaded localization data.</param>
    /// <param name="requestedLanguageCode">
    /// The language to display first, such as the stored setting or the operating system language;
    /// resolved through the catalog's manifest.
    /// </param>
    /// <param name="logger">Receives language resolution, switching and fallback events.</param>
    /// <remarks>
    /// The resolved culture is applied to the current and default threads, so date and number
    /// formatting follow the interface language rather than the operating system.
    /// </remarks>
    public LocalizationService(
        LocalizationCatalog catalog,
        string? requestedLanguageCode,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(logger);

        _catalog = catalog;
        _logger = logger;
        _currentLanguage = LanguageResolver.Resolve(requestedLanguageCode, catalog.Manifest);
        _culture = CultureInfo.GetCultureInfo(_currentLanguage);
        ApplyCulture(_culture);
        _logger.Information("Language resolved to {LanguageCode}", _currentLanguage);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return GetText(key);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LanguageOption> Languages => _catalog.Manifest.Languages;

    /// <inheritdoc />
    public string CurrentLanguage => _currentLanguage;

    /// <inheritdoc />
    public CultureInfo Culture => _culture;

    /// <inheritdoc />
    public string Format(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Format(_culture, this[key], arguments);
    }

    /// <inheritdoc />
    public bool TrySetLanguage(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        string resolved = LanguageResolver.Resolve(languageCode, _catalog.Manifest);
        if (!_catalog.TryGetStrings(resolved, out _))
        {
            _logger.Debug("Language {LanguageCode} has no readable text", resolved);
            return false;
        }

        if (string.Equals(resolved, _currentLanguage, StringComparison.Ordinal))
        {
            return true;
        }

        _currentLanguage = resolved;
        _culture = CultureInfo.GetCultureInfo(resolved);
        ApplyCulture(_culture);
        _logger.Information("Language switched to {LanguageCode}", resolved);

        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged(nameof(Culture));
        return true;
    }

    private string GetText(string key)
    {
        if (_catalog.TryGetStrings(_currentLanguage, out IReadOnlyDictionary<string, string>? current) &&
            current.TryGetValue(key, out string? text))
        {
            return text;
        }

        if (_catalog.TryGetStrings(_catalog.Manifest.Source, out IReadOnlyDictionary<string, string>? source) &&
            source.TryGetValue(key, out string? fallback))
        {
            _logger.Debug(
                "Key {Key} is missing in {Language}; using the source language",
                key,
                _currentLanguage);
            return fallback;
        }

        _logger.Debug("Key {Key} is missing in every language", key);
        return key;
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
