using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;

namespace JiuMi.ViewModels;

/// <summary>
/// View model for the settings page: the language selector and the about section.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IApplicationInfo _applicationInfo;
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settingsStore;
    private readonly ISystemLauncher _systemLauncher;
    private readonly Uri _repositoryUri;
    private LanguageOption _selectedLanguage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="applicationInfo">Product name, version and repository shown in the about section.</param>
    /// <param name="localization">The localization service that supplies interface text and languages.</param>
    /// <param name="settingsStore">The store that loads and saves the settings.</param>
    /// <param name="systemLauncher">Opens folders and links for the about section.</param>
    public SettingsViewModel(
        IApplicationInfo applicationInfo,
        ILocalizationService localization,
        ISettingsStore settingsStore,
        ISystemLauncher systemLauncher)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(systemLauncher);

        _applicationInfo = applicationInfo;
        _localization = localization;
        _settingsStore = settingsStore;
        _systemLauncher = systemLauncher;
        _repositoryUri = new Uri(applicationInfo.RepositoryUrl, UriKind.Absolute);

        Languages = localization.Languages;
        _selectedLanguage = FindLanguage(localization.CurrentLanguage) ?? Languages[0];
        AppName = applicationInfo.Name;

        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder, () => CanOpenConfigFolder);
        OpenRepositoryCommand = new RelayCommand(OpenRepository);
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// Gets the languages available for display, in manifest order.
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages
    {
        get;
    }

    /// <summary>
    /// Gets the product name shown in the about section.
    /// </summary>
    public string AppName
    {
        get;
    }

    /// <summary>
    /// Gets the version line of the about section.
    /// </summary>
    public string VersionText => _localization.Format("Settings.About.Version", _applicationInfo.Version);

    /// <summary>
    /// Gets the effective settings file path shown in the about section.
    /// </summary>
    public string ConfigPath => _settingsStore.Location.FilePath ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the settings directory can be opened.
    /// </summary>
    public bool CanOpenConfigFolder => _settingsStore.Location.DirectoryPath is not null;

    /// <summary>
    /// Gets or sets the language shown by the interface; a change applies and saves immediately.
    /// </summary>
    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || ReferenceEquals(_selectedLanguage, value))
            {
                return;
            }

            if (!_localization.TrySetLanguage(value.Code))
            {
                // The language file is unavailable: keep showing the previous selection.
                OnPropertyChanged();
                return;
            }

            _selectedLanguage = value;
            OnPropertyChanged();
            _ = _settingsStore.Save(_settingsStore.Current with
            {
                Language = value.Code,
            });
        }
    }

    /// <summary>
    /// Gets the command that opens the effective settings directory.
    /// </summary>
    public IRelayCommand OpenConfigFolderCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that opens the source repository in the default browser.
    /// </summary>
    public IRelayCommand OpenRepositoryCommand
    {
        get;
    }

    /// <inheritdoc />
    public void Dispose() => _localization.PropertyChanged -= OnLocalizationChanged;

    private LanguageOption? FindLanguage(string languageCode)
    {
        foreach (LanguageOption language in Languages)
        {
            if (string.Equals(language.Code, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                return language;
            }
        }

        return null;
    }

    private void OpenConfigFolder()
    {
        string? directory = _settingsStore.Location.DirectoryPath;
        if (directory is not null)
        {
            _systemLauncher.OpenFolder(directory);
        }
    }

    private void OpenRepository() => _systemLauncher.OpenUri(_repositoryUri);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILocalizationService.CurrentLanguage),
            StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(VersionText));

        LanguageOption? current = FindLanguage(_localization.CurrentLanguage);
        if (current is not null && !ReferenceEquals(current, _selectedLanguage))
        {
            _selectedLanguage = current;
            OnPropertyChanged(nameof(SelectedLanguage));
        }
    }
}
