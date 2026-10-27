using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using JiuMi.Application;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Notifications;
using JiuMi.Application.Shell;
using JiuMi.ViewModels;
using JiuMi.Views;

namespace JiuMi.Shell;

/// <summary>
/// Owns the window lifecycle after the splash: the tray entry, minimizing to the tray, activation from
/// another launch or from a notification, and the system notifications that replace the info bar while
/// no window is visible.
/// </summary>
/// <remarks>
/// The window references arrive on the interface thread as the startup sequence creates them; the
/// single-instance request arrives on the listener thread and is marshalled here. Subscriptions live
/// for the lifetime of the process, like the other process-wide handlers.
/// </remarks>
public sealed class ShellLifecycle
{
    private readonly ISingleInstanceCoordinator _singleInstance;
    private readonly ITrayIcon _tray;
    private readonly IUserNotification _notifications;
    private readonly IErrorReporter _errorReporter;
    private readonly IApplicationInfo _applicationInfo;
    private readonly Serilog.ILogger _logger;
    private readonly object _pendingSync = new();
    private readonly HashSet<string> _notifiedCodes = new(StringComparer.Ordinal);

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private SplashWindow? _splash;
    private MainWindow? _shell;
    private MainWindowViewModel? _viewModel;
    private ILocalizationService? _localization;
    private WindowState _restoreState = WindowState.Normal;
    private bool _hasPendingActivation;
    private bool _trayAvailable;
    private bool _minimizedHintShown;
    private bool _running;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellLifecycle"/> class.
    /// </summary>
    /// <param name="singleInstance">Delivers the activation request of a later launch.</param>
    /// <param name="tray">The notification-area entry, or the platform's no-op stand-in.</param>
    /// <param name="notifications">Delivers the short system notifications.</param>
    /// <param name="errorReporter">Supplies the records that need a notification while hidden.</param>
    /// <param name="applicationInfo">Supplies the name shown by the tray tooltip and notifications.</param>
    /// <param name="logger">Receives the lifecycle decisions.</param>
    public ShellLifecycle(
        ISingleInstanceCoordinator singleInstance,
        ITrayIcon tray,
        IUserNotification notifications,
        IErrorReporter errorReporter,
        IApplicationInfo applicationInfo,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(singleInstance);
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(logger);

        _singleInstance = singleInstance;
        _tray = tray;
        _notifications = notifications;
        _errorReporter = errorReporter;
        _applicationInfo = applicationInfo;
        _logger = logger;

        _singleInstance.ActivationRequested += OnActivationRequested;
        _tray.ShowRequested += OnTrayShowRequested;
        _tray.SettingsRequested += OnTraySettingsRequested;
        _tray.ExitRequested += OnTrayExitRequested;
    }

    /// <summary>
    /// Records the lifetime that owns the shell window and the shutdown rule.
    /// </summary>
    /// <param name="desktop">The desktop lifetime.</param>
    public void AttachLifetime(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        _desktop = desktop;
    }

    /// <summary>
    /// Registers the splash as the activation target and consumes a request that arrived before it.
    /// </summary>
    /// <param name="splash">The visible splash window.</param>
    public void AttachSplash(SplashWindow splash)
    {
        ArgumentNullException.ThrowIfNull(splash);
        _splash = splash;
        TryConsumePendingActivation();
    }

    /// <summary>
    /// Forgets a splash that left the screen.
    /// </summary>
    /// <param name="splash">The splash window that is closing.</param>
    public void DetachSplash(SplashWindow splash)
    {
        ArgumentNullException.ThrowIfNull(splash);
        if (ReferenceEquals(_splash, splash))
        {
            _splash = null;
        }
    }

    /// <summary>
    /// Takes over once the shell window is on screen: creates the tray entry and consumes a pending
    /// activation request.
    /// </summary>
    /// <param name="shell">The visible main window.</param>
    public void AttachShell(MainWindow shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        if (_running)
        {
            return;
        }

        _running = true;
        _shell = shell;
        _viewModel = shell.DataContext as MainWindowViewModel;
        _localization = _viewModel?.Localization;

        if (_localization is not null)
        {
            _localization.PropertyChanged += OnLocalizationChanged;
        }

        shell.PropertyChanged += OnShellPropertyChanged;
        _errorReporter.Reported += OnErrorReported;
        _trayAvailable = _tray.Show(_applicationInfo.Name, CreateTrayMenu());

        if (_trayAvailable)
        {
            _logger.Information("Tray icon shown");
        }
        else
        {
            _logger.Debug("Tray icon is unavailable; minimizing keeps the window in the taskbar");
        }

        TryConsumePendingActivation();
    }

    /// <summary>
    /// Consumes a pending activation request once startup has failed and only the error window is left.
    /// </summary>
    public void NotifyStartupFaulted() => TryConsumePendingActivation();

    private void OnActivationRequested(object? sender, EventArgs e)
    {
        lock (_pendingSync)
        {
            _hasPendingActivation = true;
        }

        _logger.Information("Activation requested by another launch");

        // The listener runs on its own thread; the decision needs the interface thread.
        Dispatcher.UIThread.Post(TryConsumePendingActivation);
    }

    private void TryConsumePendingActivation()
    {
        lock (_pendingSync)
        {
            if (!_hasPendingActivation)
            {
                return;
            }

            _hasPendingActivation = false;
        }

        if (!TryActivate())
        {
            // No target yet: the request has to survive until the splash or the shell exists.
            lock (_pendingSync)
            {
                _hasPendingActivation = true;
            }
        }
    }

    private bool TryActivate()
    {
        if (_splash is { IsVisible: true } splash)
        {
            splash.Activate();
            return true;
        }

        if (_shell is { IsLoaded: true } shell)
        {
            ActivateShell(shell);
            return true;
        }

        // A visible window without a usable shell can only be the error window.
        if (_desktop?.Windows.FirstOrDefault(window => window.IsVisible) is { } visible)
        {
            visible.Activate();
            return true;
        }

        return false;
    }

    private void ActivateShell(MainWindow shell)
    {
        if (!shell.IsVisible || shell.WindowState == WindowState.Minimized)
        {
            RestoreShell(shell);
            return;
        }

        shell.Activate();
    }

    private void RestoreShell(MainWindow shell)
    {
        shell.ShowInTaskbar = true;
        shell.WindowState = _restoreState;
        shell.Show();
        shell.Activate();
        _logger.Information("Shell restored from the tray");
    }

    private void OnShellPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != Window.WindowStateProperty ||
            change.GetNewValue<WindowState>() != WindowState.Minimized)
        {
            return;
        }

        // The minimized state replaces the geometry the window had; remembering whether it was
        // maximized is what lets the restore return to the same shape.
        _restoreState = change.GetOldValue<WindowState>() == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;

        if (sender is MainWindow shell)
        {
            Dispatcher.UIThread.Post(() => HideToTray(shell));
        }
    }

    private void HideToTray(MainWindow shell)
    {
        if (!_trayAvailable || !ReferenceEquals(shell, _shell) || !shell.IsVisible)
        {
            // Without a tray entry, hiding would leave no way back; the taskbar entry stays.
            return;
        }

        shell.ShowInTaskbar = false;
        shell.Hide();
        _logger.Information("Shell hidden to the tray");

        if (_minimizedHintShown)
        {
            return;
        }

        _minimizedHintShown = true;
        ShowNotification(Localized("Shell.Tray.MinimizedHint"));
    }

    private void OnTrayShowRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => TryActivate());

    private void OnTraySettingsRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (TryActivate())
            {
                _viewModel?.ShowSettings();
            }
        });

    private void OnTrayExitRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RequestExit);

    private void RequestExit()
    {
        if (_shell is { } shell)
        {
            // The close button is the exit path, so the tray reuses it instead of shutting down behind
            // the window and skipping the save on exit.
            shell.Close();
            return;
        }

        _desktop?.Shutdown(ExitCodes.Success);
    }

    private void OnErrorReported(object? sender, ErrorRecord record) =>
        Dispatcher.UIThread.Post(() => NotifyHiddenError(record));

    private void NotifyHiddenError(ErrorRecord record)
    {
        if (_shell is not { IsVisible: false } || _localization is null || _desktop is null)
        {
            return;
        }

        // The error window is a visible window of its own; it replaces the notification.
        if (_desktop.Windows.Any(window => window.IsVisible))
        {
            return;
        }

        ErrorPresentation? presentation = ErrorPresentationCatalog.Find(record.Code);
        if (presentation is not { Carrier: ErrorCarrier.InfoBar, Level: ErrorLevel.Error } ||
            !_notifiedCodes.Add(record.Code))
        {
            // The info bar merges repeats of a code; the notification follows the same rule so a
            // failure that keeps recurring while hidden cannot fill the notification area.
            return;
        }

        ShowNotification(Localized("Error." + record.Code));
    }

    private void ShowNotification(string message)
    {
        if (message.Length == 0)
        {
            return;
        }

        _notifications.Show(_applicationInfo.Name, message, ActivateFromNotification);
    }

    private void ActivateFromNotification() => Dispatcher.UIThread.Post(() => TryActivate());

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_trayAvailable)
        {
            _tray.Show(_applicationInfo.Name, CreateTrayMenu());
        }
    }

    private TrayMenuText CreateTrayMenu() => new(
        Localized("Shell.Tray.Show"),
        Localized("Shell.Tray.Settings"),
        Localized("Shell.Tray.Exit"));

    private string Localized(string key) => _localization?[key] ?? string.Empty;
}
