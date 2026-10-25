using Avalonia.Controls.ApplicationLifetimes;
using JiuMi.Application.Errors;
using JiuMi.Errors;
using JiuMi.Views;

namespace JiuMi.Startup;

/// <summary>
/// Starts the splash sequence with the media and the dependencies it reports through.
/// </summary>
public sealed class SplashStartup : IShellStartup
{
    private readonly ShellInitialization _initialization;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly IErrorWindowPresenter _errorWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplashStartup"/> class.
    /// </summary>
    /// <param name="initialization">The startup work the splash covers.</param>
    /// <param name="errorReporter">Records the startup warnings and late completion.</param>
    /// <param name="recordFactory">Creates those records with the shared environment fields.</param>
    /// <param name="errorWindows">Presents a startup failure that blocks the shell.</param>
    public SplashStartup(
        ShellInitialization initialization,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        IErrorWindowPresenter errorWindows)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(errorWindows);

        _initialization = initialization;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _errorWindows = errorWindows;
    }

    /// <inheritdoc />
    public void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        new SplashSequence(
            desktop,
            _initialization,
            SplashMediaLoader.Load(),
            static () => new SplashWindow(),
            _errorReporter,
            _recordFactory,
            _errorWindows).Start();
    }
}
