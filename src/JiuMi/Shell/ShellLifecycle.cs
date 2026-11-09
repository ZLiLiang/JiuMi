using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using JiuMi.Application;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Notifications;
using JiuMi.Application.Settings;
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
    private readonly ISettingsStore _settingsStore;
    private readonly ErrorRecordFactory _recordFactory;
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
    private bool _running;
    private bool _allowClose;
    private bool _closeDialogOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellLifecycle"/> class.
    /// </summary>
    /// <param name="singleInstance">Delivers the activation request of a later launch.</param>
    /// <param name="tray">The notification-area entry, or the platform's no-op stand-in.</param>
    /// <param name="notifications">Delivers the short system notifications.</param>
    /// <param name="errorReporter">Supplies the records that need a notification while hidden.</param>
    /// <param name="applicationInfo">Supplies the name shown by the tray tooltip and notifications.</param>
    /// <param name="settingsStore">Supplies and stores the close behavior.</param>
    /// <param name="recordFactory">Creates the records for a failed close-behavior save.</param>
    /// <param name="logger">Receives the lifecycle decisions.</param>
    public ShellLifecycle(
        ISingleInstanceCoordinator singleInstance,
        ITrayIcon tray,
        IUserNotification notifications,
        IErrorReporter errorReporter,
        IApplicationInfo applicationInfo,
        ISettingsStore settingsStore,
        ErrorRecordFactory recordFactory,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(singleInstance);
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _singleInstance = singleInstance;
        _tray = tray;
        _notifications = notifications;
        _errorReporter = errorReporter;
        _applicationInfo = applicationInfo;
        _settingsStore = settingsStore;
        _recordFactory = recordFactory;
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
        shell.Closing += OnShellClosing;
        _errorReporter.Reported += OnErrorReported;
        _trayAvailable = _tray.Show(_applicationInfo.Name, CreateTrayMenu());

        if (_trayAvailable)
        {
            _logger.Information("Tray icon shown");
        }
        else
        {
            _logger.Debug(
                "Tray icon is unavailable; closing the window exits and minimizing keeps it in the taskbar");
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
        _logger.Information("Shell restored");
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
    }

    private void HideToTray(MainWindow shell)
    {
        if (!_trayAvailable || !ReferenceEquals(shell, _shell) || !shell.IsVisible)
        {
            // Without a tray entry, hiding would leave no way back; the taskbar entry stays.
            return;
        }

        // The close path can hide a normal or maximized window; a minimized window keeps the shape
        // recorded when it was minimized.
        if (shell.WindowState != WindowState.Minimized)
        {
            _restoreState = shell.WindowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        shell.ShowInTaskbar = false;
        shell.Hide();
        _logger.Information("Shell hidden to the tray");
    }

    private void OnShellClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not MainWindow shell)
        {
            return;
        }

        // An explicit exit, an OS shutdown or a missing tray bypasses the question.
        if (_allowClose ||
            e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown ||
            !_trayAvailable)
        {
            SaveOnExit();
            return;
        }

        CloseAction action = _settingsStore.Current.CloseAction;
        if (action == CloseAction.Exit)
        {
            SaveOnExit();
            return;
        }

        // The window survives this pass: either it is hidden after the event, or the dialog decides.
        e.Cancel = true;
        if (action == CloseAction.MinimizeToTray)
        {
            Dispatcher.UIThread.Post(() => HideToTray(shell));
            return;
        }

        if (_localization is not { } localization)
        {
            // Without text the question cannot be shown; letting the close through is the only way out.
            e.Cancel = false;
            SaveOnExit();
            return;
        }

        if (_closeDialogOpen)
        {
            return;
        }

        _closeDialogOpen = true;
        Dispatcher.UIThread.Post(() => _ = ShowCloseDialogAsync(shell, localization));
    }

    private async Task ShowCloseDialogAsync(MainWindow shell, ILocalizationService localization)
    {
        try
        {
            if (!shell.IsVisible || shell.WindowState == WindowState.Minimized)
            {
                RestoreShell(shell);
            }

            var model = new CloseConfirmWindowViewModel(
                _applicationInfo.Name,
                localization.Format("Shell.Close.Title", _applicationInfo.Name),
                localization["Shell.Close.Body"],
                localization["Shell.Close.DontAsk"],
                localization["Shell.Close.Cancel"],
                localization["Shell.Close.Exit"],
                localization["Shell.Close.HideToTray"]);
            var dialog = new CloseConfirmWindow { DataContext = model };

            CloseConfirmResult? result = await dialog.ShowDialog<CloseConfirmResult>(shell);
            ApplyCloseResult(shell, result);
        }
        catch (Exception exception)
        {
            // A dialog that cannot be shown must not take the window down.
            _logger.Warning(exception, "The close confirmation failed; the window stays open");
        }
        finally
        {
            _closeDialogOpen = false;
        }
    }

    private void ApplyCloseResult(MainWindow shell, CloseConfirmResult? result)
    {
        if (result is null || result.Action == CloseConfirmAction.Cancel)
        {
            return;
        }

        if (result.DontAsk)
        {
            SaveCloseAction(result.Action);
        }

        if (result.Action == CloseConfirmAction.Exit)
        {
            _allowClose = true;
            shell.Close();
            return;
        }

        HideToTray(shell);
    }

    private void SaveCloseAction(CloseConfirmAction action)
    {
        CloseAction stored = action == CloseConfirmAction.HideToTray
            ? CloseAction.MinimizeToTray
            : CloseAction.Exit;
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            CloseAction = stored,
        });

        if (result.Error is { } error)
        {
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.CloseAction",
                error));
            return;
        }

        _logger.Debug("Close action saved as {CloseAction}", stored);
    }

    private void SaveOnExit() => _viewModel?.SaveOnExit();

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
            // The tray exit is explicit: it skips the close question but still saves on the way out.
            _allowClose = true;
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
