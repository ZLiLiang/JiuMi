using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JiuMi.Errors;
using JiuMi.Startup;

namespace JiuMi;

/// <summary>
/// Application entry point after the Avalonia runtime is initialized.
/// </summary>
public partial class App : Avalonia.Application
{
    private readonly IShellStartup _startup;
    private readonly IErrorWindowPresenter _errorWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader; the host uses the factory overload.</remarks>
    public App()
        : this(NullShellStartup.Instance, NullErrorWindowPresenter.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="startup">Puts the startup sequence on screen and starts the work behind it.</param>
    /// <param name="errorWindows">Presents exceptions that escape the UI thread dispatcher.</param>
    public App(IShellStartup startup, IErrorWindowPresenter errorWindows)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(errorWindows);
        _startup = startup;
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
            // The shell window is assigned by the sequence before it closes the splash, which is what
            // keeps this shutdown rule from ending the process at the handoff.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            _startup.Start(desktop);
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
