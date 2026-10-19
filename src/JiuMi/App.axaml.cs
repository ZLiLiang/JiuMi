using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JiuMi.Views;

namespace JiuMi;

/// <summary>
/// Application entry point after the Avalonia runtime is initialized.
/// </summary>
public partial class App : Avalonia.Application
{
    private readonly Func<MainWindow> _createMainWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader; the host uses the factory overload.</remarks>
    public App()
        : this(static () => new MainWindow())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="createMainWindow">Creates the shell window once the framework is ready.</param>
    public App(Func<MainWindow> createMainWindow)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);
        _createMainWindow = createMainWindow;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = _createMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
