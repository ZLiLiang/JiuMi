using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Logging;
using JiuMi.Application.Settings;
using JiuMi.Diagnostics;
using JiuMi.Threading;

namespace JiuMi.ViewModels;

/// <summary>
/// View model for the settings page: language, appearance and the about section.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IApplicationInfo _applicationInfo;
    private readonly IDiagnosticsExporter _diagnosticsExporter;
    private readonly IErrorReporter _errorReporter;
    private readonly DiagnosticsExportController _exportController;
    private readonly ILoggingStatus _loggingStatus;
    private readonly Serilog.ILogger _logger;
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
    /// <param name="diagnosticsExporter">Supplies the default bundle name and directory.</param>
    /// <param name="exportController">Runs the export and reports its failures.</param>
    /// <param name="loggingStatus">Reports whether file logging is working.</param>
    /// <param name="logger">Receives settings saves from this page.</param>
    public SettingsViewModel(
        IApplicationInfo applicationInfo,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        ILocalizationService localization,
        ISettingsStore settingsStore,
        IThemeService themeService,
        IUiThreadDispatcher uiThreadDispatcher,
        ISystemLauncher systemLauncher,
        IDiagnosticsExporter diagnosticsExporter,
        DiagnosticsExportController exportController,
        ILoggingStatus loggingStatus,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(systemLauncher);
        ArgumentNullException.ThrowIfNull(diagnosticsExporter);
        ArgumentNullException.ThrowIfNull(exportController);
        ArgumentNullException.ThrowIfNull(loggingStatus);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationInfo = applicationInfo;
        _diagnosticsExporter = diagnosticsExporter;
        _errorReporter = errorReporter;
        _exportController = exportController;
        _loggingStatus = loggingStatus;
        _logger = logger;
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
        _themeService.PropertyChanged += OnThemeServiceChanged;
        _loggingStatus.PropertyChanged += OnLoggingStatusChanged;
        _exportController.ExportingChanged += OnExportingChanged;
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
    /// Gets a value indicating whether file logging is currently working.
    /// </summary>
    public bool IsFileLoggingAvailable => _loggingStatus.IsFileLoggingAvailable;

    /// <summary>
    /// Gets a value indicating whether a diagnostics export is running.
    /// </summary>
    public bool IsExporting => _exportController.IsExporting;

    /// <summary>
    /// Gets a value indicating whether the export button can start a new export.
    /// </summary>
    public bool CanExportDiagnostics => !IsExporting;

    /// <summary>
    /// Gets the file name the save dialog starts with.
    /// </summary>
    public string DefaultDiagnosticsFileName => _diagnosticsExporter.CreateDefaultFileName();

    /// <summary>
    /// Gets the directory the save dialog starts in, or <see langword="null"/> when there is no log directory.
    /// </summary>
    public string? DiagnosticsDirectory => _diagnosticsExporter.LogDirectory;

    /// <summary>
    /// Gets the label the save dialog shows for the zip file type.
    /// </summary>
    public string DiagnosticsFileTypeName => _localization["Settings.About.DiagnosticsFileType"];

    /// <summary>
    /// Exports the diagnostics bundle to the chosen path.
    /// </summary>
    /// <param name="destinationPath">The zip file the user chose.</param>
    /// <returns>The outcome, with failures already reported.</returns>
    public Task<DiagnosticsExportResult> ExportDiagnosticsAsync(string destinationPath) =>
        _exportController.ExportAsync(destinationPath);

    /// <summary>
    /// Reports a failure that happened before the export started, such as a save-dialog error.
    /// </summary>
    /// <param name="exception">The failure.</param>
    public void ReportDiagnosticsFailure(Exception exception) => _exportController.ReportFailure(exception);

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
    /// Gets or sets a value indicating whether the tools may fetch data from the network.
    /// </summary>
    public bool IsOnlineDataEnabled
    {
        get => _settingsStore.Current.OnlineDataEnabled;
        set
        {
            if (value == _settingsStore.Current.OnlineDataEnabled)
            {
                return;
            }

            SaveSettings(_settingsStore.Current with
            {
                OnlineDataEnabled = value,
            });
            OnPropertyChanged();
        }
    }

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
    public void Dispose()
    {
        _localization.PropertyChanged -= OnLocalizationChanged;
        _themeService.PropertyChanged -= OnThemeServiceChanged;
        _loggingStatus.PropertyChanged -= OnLoggingStatusChanged;
        _exportController.ExportingChanged -= OnExportingChanged;
    }

    private void SelectTheme(AppTheme theme)
    {
        if (theme == _themeService.Current)
        {
            // Startup may fall back to a theme other than the stored one; selecting what is on screen
            // must still make it the stored preference.
            if (theme != _settingsStore.Current.Theme)
            {
                SaveTheme(theme);
            }

            return;
        }

        bool applied = _themeService.TrySetTheme(theme, out Exception? failure);
        NotifyThemeSelectionChanged();
        if (!applied)
        {
            // The requested palette is unavailable and the previous preference is back in effect.
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.ThemeSwitchFailed,
                "Theme.Switch",
                failure));
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
            return;
        }

        if (result.Persisted)
        {
            _logger.Debug("Settings saved");
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

    private void OnThemeServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(IThemeService.Current), StringComparison.Ordinal))
        {
            // The applied theme can change after the shell is up when startup finishes late.
            NotifyThemeSelectionChanged();
        }
    }

    private void OnLoggingStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILoggingStatus.IsFileLoggingAvailable),
            StringComparison.Ordinal))
        {
            return;
        }

        // Writes can fail on any thread; the about section is only safe to update on the UI thread.
        _uiThreadDispatcher.Post(() => OnPropertyChanged(nameof(IsFileLoggingAvailable)));
    }

    // The controller documents ExportingChanged as an interface-thread event, so no marshalling here.
    private void OnExportingChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsExporting));
        OnPropertyChanged(nameof(CanExportDiagnostics));
    }
}
