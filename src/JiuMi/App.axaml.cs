using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JiuMi.Application.Settings;
using JiuMi.Errors;
using JiuMi.Theming;
using JiuMi.Views;

namespace JiuMi;

/// <summary>
/// Application entry point after the Avalonia runtime is initialized.
/// </summary>
public partial class App : Avalonia.Application
{
    private readonly Func<MainWindow> _createMainWindow;
    private readonly IErrorWindowPresenter _errorWindows;
    private readonly IThemeService _themeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader; the host uses the factory overload.</remarks>
    public App()
        : this(static () => new MainWindow(), NullErrorWindowPresenter.Instance, NullThemeService.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="createMainWindow">Creates the shell window once the framework is ready.</param>
    /// <param name="errorWindows">Presents exceptions that escape the UI thread dispatcher.</param>
    /// <param name="themeService">Applies the persisted theme before the shell window is created.</param>
    public App(
        Func<MainWindow> createMainWindow,
        IErrorWindowPresenter errorWindows,
        IThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);
        ArgumentNullException.ThrowIfNull(errorWindows);
        ArgumentNullException.ThrowIfNull(themeService);
        _createMainWindow = createMainWindow;
        _errorWindows = errorWindows;
        _themeService = themeService;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        // The application lives as long as the process, so the subscription is deliberately never removed.
        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
        _themeService.ApplyStoredTheme();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = _createMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the dispatcher alive: the error window is the user-facing outcome, not a crash.
        e.Handled = true;
        _errorWindows.PresentUnhandledException(e.Exception);
    }
}
