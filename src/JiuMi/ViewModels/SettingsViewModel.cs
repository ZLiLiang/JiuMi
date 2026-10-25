using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Threading;

namespace JiuMi.ViewModels;

/// <summary>
/// View model for the settings page: language, appearance and the about section.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IApplicationInfo _applicationInfo;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settingsStore;
    private readonly ISystemLauncher _systemLauncher;
    private readonly IThemeService _themeService;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly Uri _repositoryUri;
    private LanguageOption _selectedLanguage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="applicationInfo">Product name, version and repository shown in the about section.</param>
    /// <param name="errorReporter">Receives language switch and settings save failures.</param>
    /// <param name="recordFactory">Creates the records reported from this page.</param>
    /// <param name="localization">The localization service that supplies interface text and languages.</param>
    /// <param name="settingsStore">The store that loads and saves the settings.</param>
    /// <param name="themeService">Applies theme changes and reports the preference in effect.</param>
    /// <param name="uiThreadDispatcher">Queues the theme selection update after the radio click finishes.</param>
    /// <param name="systemLauncher">Opens folders and links for the about section.</param>
    public SettingsViewModel(
        IApplicationInfo applicationInfo,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        ILocalizationService localization,
        ISettingsStore settingsStore,
        IThemeService themeService,
        IUiThreadDispatcher uiThreadDispatcher,
        ISystemLauncher systemLauncher)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(systemLauncher);

        _applicationInfo = applicationInfo;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _localization = localization;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _uiThreadDispatcher = uiThreadDispatcher;
        _systemLauncher = systemLauncher;
        _repositoryUri = new Uri(applicationInfo.RepositoryUrl, UriKind.Absolute);

        Languages = localization.Languages;
        _selectedLanguage = FindLanguage(localization.CurrentLanguage) ?? Languages[0];
        AppName = applicationInfo.Name;

        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder, () => CanOpenConfigFolder);
        OpenRepositoryCommand = new RelayCommand(OpenRepository);
        SelectThemeCommand = new RelayCommand<AppTheme>(SelectTheme);
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
                _errorReporter.Report(_recordFactory.Create(
                    ErrorCodes.LocalizationSwitchFailed,
                    "Localization.Switch"));
                OnPropertyChanged();
                return;
            }

            _selectedLanguage = value;
            OnPropertyChanged();

            SaveSettings(_settingsStore.Current with
            {
                Language = value.Code,
            });
        }
    }

    /// <summary>
    /// Gets a value indicating whether the interface follows the operating system theme.
    /// </summary>
    public bool IsFollowSystemTheme => _themeService.Current == AppTheme.System;

    /// <summary>
    /// Gets a value indicating whether the light theme is selected.
    /// </summary>
    public bool IsLightTheme => _themeService.Current == AppTheme.Light;

    /// <summary>
    /// Gets a value indicating whether the dark theme is selected.
    /// </summary>
    public bool IsDarkTheme => _themeService.Current == AppTheme.Dark;

    /// <summary>
    /// Gets the command that switches the appearance theme.
    /// </summary>
    public IRelayCommand<AppTheme> SelectThemeCommand
    {
        get;
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

    private void SelectTheme(AppTheme theme)
    {
        if (theme == _themeService.Current)
        {
            // A rejected startup theme can leave the applied preference behind the stored one; record
            // the choice so the next start does not retry the unavailable theme.
            if (_settingsStore.Current.Theme != theme)
            {
                SaveTheme(theme);
            }

            return;
        }

        bool applied = _themeService.TrySetTheme(theme);
        NotifyThemeSelectionChanged();
        if (!applied)
        {
            // The requested palette is unavailable and the previous preference is back in effect.
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.ThemeSwitchFailed,
                "Theme.Switch"));
            return;
        }

        SaveTheme(theme);
    }

    private void SaveTheme(AppTheme theme) =>
        SaveSettings(_settingsStore.Current with
        {
            Theme = theme,
        });

    private void SaveSettings(AppSettings settings)
    {
        SettingsSaveResult result = _settingsStore.Save(settings);
        if (result.Error is { } error)
        {
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.Save",
                error));
        }
    }

    private void NotifyThemeSelectionChanged()
    {
        // The radio group finishes the click after the command returns, so push the selection on a later
        // turn; otherwise the clicked radio overwrites a rejected change.
        _uiThreadDispatcher.Post(
            () =>
            {
                OnPropertyChanged(nameof(IsFollowSystemTheme));
                OnPropertyChanged(nameof(IsLightTheme));
                OnPropertyChanged(nameof(IsDarkTheme));
            });
    }

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
