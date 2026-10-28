using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;

namespace JiuMi.ViewModels;

/// <summary>
/// View model for the shell window: navigation, the current page and the application identity.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const double ExpandedNavigationWidth = 280;
    private const double CollapsedNavigationWidth = 56;

    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settingsStore;
    private readonly Serilog.ILogger _logger;
    private readonly ShellNavigationItem _settingsNavigationItem;
    private readonly ShellNavigationItem _worldClockNavigationItem;
    private readonly WorldClockViewModel _worldClockPage;
    private ShellNavigationItem? _selectedPrimaryItem;
    private ShellNavigationItem? _selectedSettingsItem;
    private ViewModelBase _currentPage;
    private bool _isNavigationExpanded;
    private bool _savedOnExit;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="applicationInfo">Application name and version shown by the shell.</param>
    /// <param name="localization">The localization service that supplies interface text.</param>
    /// <param name="settingsPage">The settings page whose sections are bound from its view.</param>
    /// <param name="worldClockPage">The world working hours page.</param>
    /// <param name="infoBar">The application-wide error notice shown above the content.</param>
    /// <param name="settingsStore">The store that holds the navigation state and saves it on exit.</param>
    /// <param name="logger">Receives navigation and window-lifecycle events.</param>
    public MainWindowViewModel(
        IApplicationInfo applicationInfo,
        ILocalizationService localization,
        SettingsViewModel settingsPage,
        WorldClockViewModel worldClockPage,
        InfoBarViewModel infoBar,
        ISettingsStore settingsStore,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settingsPage);
        ArgumentNullException.ThrowIfNull(worldClockPage);
        ArgumentNullException.ThrowIfNull(infoBar);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(logger);

        AppName = applicationInfo.Name;
        AppVersion = applicationInfo.Version;
        _localization = localization;
        _settingsStore = settingsStore;
        _logger = logger;
        _worldClockPage = worldClockPage;
        _isNavigationExpanded = settingsStore.Current.NavExpanded;

        HomePage = new HomeViewModel();
        HomePage.OpenWorldClockCommand = new RelayCommand(OpenWorldClock);
        SettingsPage = settingsPage;
        InfoBar = infoBar;
        _currentPage = HomePage;

        _worldClockNavigationItem = CreateItem(
            "world-clock", "Tool.WorldClock.Name", "Clock", "ClockFilled", ShellPage.WorldClock, isEnabled: true);
        PrimaryNavigationItems =
        [
            CreateItem("home", "Shell.Nav.Home", "Home", "HomeFilled", ShellPage.Home, isEnabled: true),
            CreateItem("wechat-download", "Tool.WeChatDownload.Name", "Video", "VideoFilled", page: null, isEnabled: false),
            CreateItem("batch-rename", "Tool.BatchRename.Name", "Rename", "Rename", page: null, isEnabled: false),
            CreateItem("icon-convert", "Tool.IconConvert.Name", "Image", "ImageFilled", page: null, isEnabled: false),
            _worldClockNavigationItem,
        ];
        _settingsNavigationItem = CreateItem(
            "settings", "Shell.Nav.Settings", "Settings", "SettingsFilled", ShellPage.Settings, isEnabled: true);
        SettingsNavigationItems = [_settingsNavigationItem];

        _selectedPrimaryItem = PrimaryNavigationItems[0];
        ToggleNavigationCommand = new RelayCommand(ToggleNavigation);
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// Gets the product name shown in the title bar.
    /// </summary>
    public string AppName
    {
        get;
    }

    /// <summary>
    /// Gets the semantic version shown on the settings page.
    /// </summary>
    public string AppVersion
    {
        get;
    }

    /// <summary>
    /// Gets the localization service bound from the views.
    /// </summary>
    public ILocalizationService Localization => _localization;

    /// <summary>
    /// Gets the navigation items above the flexible space, starting with the home item.
    /// </summary>
    public IReadOnlyList<ShellNavigationItem> PrimaryNavigationItems
    {
        get;
    }

    /// <summary>
    /// Gets the navigation items pinned to the bottom of the rail.
    /// </summary>
    public IReadOnlyList<ShellNavigationItem> SettingsNavigationItems
    {
        get;
    }

    /// <summary>
    /// Gets or sets the selected item of the upper navigation group.
    /// </summary>
    public ShellNavigationItem? SelectedPrimaryItem
    {
        get => _selectedPrimaryItem;
        set
        {
            if (!SetProperty(ref _selectedPrimaryItem, value) || value is null)
            {
                return;
            }

            SelectedSettingsItem = null;
            NavigateTo(value.Page);
        }
    }

    /// <summary>
    /// Gets or sets the selected item of the pinned navigation group.
    /// </summary>
    public ShellNavigationItem? SelectedSettingsItem
    {
        get => _selectedSettingsItem;
        set
        {
            if (!SetProperty(ref _selectedSettingsItem, value) || value is null)
            {
                return;
            }

            SelectedPrimaryItem = null;
            NavigateTo(value.Page);
        }
    }

    /// <summary>
    /// Gets the page shown in the content area.
    /// </summary>
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the navigation rail shows labels.
    /// </summary>
    public bool IsNavigationExpanded
    {
        get => _isNavigationExpanded;
        set
        {
            if (!SetProperty(ref _isNavigationExpanded, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNavigationCollapsed));
            OnPropertyChanged(nameof(NavigationWidth));
            OnPropertyChanged(nameof(ToggleNavigationAccessibleName));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the navigation rail shows icons only.
    /// </summary>
    public bool IsNavigationCollapsed => !_isNavigationExpanded;

    /// <summary>
    /// Gets the width of the navigation rail.
    /// </summary>
    public double NavigationWidth =>
        _isNavigationExpanded ? ExpandedNavigationWidth : CollapsedNavigationWidth;

    /// <summary>
    /// Gets the accessible name of the expand/collapse button for its current state.
    /// </summary>
    public string ToggleNavigationAccessibleName =>
        _localization[_isNavigationExpanded ? "Shell.Nav.Collapse" : "Shell.Nav.Expand"];

    /// <summary>
    /// Gets the command that expands or collapses the navigation rail.
    /// </summary>
    public IRelayCommand ToggleNavigationCommand
    {
        get;
    }

    /// <summary>
    /// Gets the page that hosts the home content.
    /// </summary>
    public HomeViewModel HomePage
    {
        get;
    }

    /// <summary>
    /// Gets the page that hosts the settings content.
    /// </summary>
    public SettingsViewModel SettingsPage
    {
        get;
    }

    /// <summary>
    /// Gets the page that hosts the world working hours.
    /// </summary>
    public WorldClockViewModel WorldClockPage => _worldClockPage;

    /// <summary>
    /// Gets the application-wide error notice shown above the content.
    /// </summary>
    public InfoBarViewModel InfoBar
    {
        get;
    }

    /// <summary>
    /// Saves the navigation rail state as the main window closes; a failed write stays silent.
    /// </summary>
    public void SaveOnExit()
    {
        if (_savedOnExit)
        {
            return;
        }

        _savedOnExit = true;
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            NavExpanded = IsNavigationExpanded,
        });

        if (result.Error is { } error)
        {
            _logger
                .ForContext("operation", "Settings.SaveOnExit")
                .Warning(error, "Settings save on exit failed");
        }
    }

    /// <summary>
    /// Recomputes the current page when the window is activated again.
    /// </summary>
    public void OnWindowActivated()
    {
        if (ReferenceEquals(CurrentPage, _worldClockPage))
        {
            _worldClockPage.Refresh();
        }
    }

    /// <summary>
    /// Opens the settings page through the same navigation the rail uses; an already open page stays put.
    /// </summary>
    public void ShowSettings()
    {
        if (ReferenceEquals(CurrentPage, SettingsPage))
        {
            return;
        }

        SelectedSettingsItem = _settingsNavigationItem;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _localization.PropertyChanged -= OnLocalizationChanged;
        _worldClockPage.Dispose();
        SettingsPage.Dispose();
        InfoBar.Dispose();
    }

    private ShellNavigationItem CreateItem(
        string id,
        string labelKey,
        string iconName,
        string selectedIconName,
        ShellPage? page,
        bool isEnabled) =>
        new(
            id,
            labelKey,
            iconName,
            selectedIconName,
            page,
            isEnabled,
            isComingSoon: !isEnabled,
            _localization);

    private void ToggleNavigation()
    {
        IsNavigationExpanded = !IsNavigationExpanded;
        _logger.Debug("Navigation expanded state changed to {IsExpanded}", IsNavigationExpanded);
    }

    private void NavigateTo(ShellPage? page)
    {
        ViewModelBase target = page switch
        {
            ShellPage.Settings => SettingsPage,
            ShellPage.WorldClock => _worldClockPage,
            _ => HomePage,
        };

        if (ReferenceEquals(target, _worldClockPage))
        {
            _worldClockPage.Activate();
        }
        else
        {
            _worldClockPage.Deactivate();
        }

        CurrentPage = target;
        _logger.Debug("Navigated to {Page}", page);
    }

    private void OpenWorldClock()
    {
        SelectedPrimaryItem = _worldClockNavigationItem;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Localization));
        OnPropertyChanged(nameof(ToggleNavigationAccessibleName));

        foreach (ShellNavigationItem item in PrimaryNavigationItems)
        {
            item.RefreshLabel();
        }

        foreach (ShellNavigationItem item in SettingsNavigationItems)
        {
            item.RefreshLabel();
        }
    }
}
