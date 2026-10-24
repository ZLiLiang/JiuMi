using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JiuMi.Errors;
using JiuMi.Views;

namespace JiuMi;

/// <summary>
/// Application entry point after the Avalonia runtime is initialized.
/// </summary>
public partial class App : Avalonia.Application
{
    private readonly Func<MainWindow> _createMainWindow;
    private readonly IErrorWindowPresenter _errorWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader; the host uses the factory overload.</remarks>
    public App()
        : this(static () => new MainWindow(), NullErrorWindowPresenter.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="createMainWindow">Creates the shell window once the framework is ready.</param>
    /// <param name="errorWindows">Presents exceptions that escape the UI thread dispatcher.</param>
    public App(Func<MainWindow> createMainWindow, IErrorWindowPresenter errorWindows)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);
        ArgumentNullException.ThrowIfNull(errorWindows);
        _createMainWindow = createMainWindow;
        _errorWindows = errorWindows;
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
