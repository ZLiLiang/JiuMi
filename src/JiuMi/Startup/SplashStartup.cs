using Avalonia.Controls.ApplicationLifetimes;
using JiuMi.Application.Errors;
using JiuMi.Application.Startup;
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
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplashStartup"/> class.
    /// </summary>
    /// <param name="initialization">The startup work the splash covers.</param>
    /// <param name="errorReporter">Records the startup warnings and late completion.</param>
    /// <param name="recordFactory">Creates those records with the shared environment fields.</param>
    /// <param name="errorWindows">Presents a startup failure that blocks the shell.</param>
    /// <param name="logger">Logs each startup stage.</param>
    public SplashStartup(
        ShellInitialization initialization,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        IErrorWindowPresenter errorWindows,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(errorWindows);
        ArgumentNullException.ThrowIfNull(logger);

        _initialization = initialization;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _errorWindows = errorWindows;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        SplashMedia media = SplashMediaLoader.Load();
        var session = new StartupSession(SplashHandoffGate.ForGif(media.Duration));
        session.StateChanged += (_, state) => _logger.Information("Startup state {State}", state);

        // TransitionTo suppresses a transition to the state already in effect, and the session starts
        // in Bootstrapping, so the initial state logs directly rather than duplicating in the event.
        _logger.Information("Startup state {State}", StartupState.Bootstrapping);

        new SplashSequence(
            desktop,
            _initialization,
            media,
            session,
            static () => new SplashWindow(),
            _errorReporter,
            _recordFactory,
            _errorWindows).Start();
    }
}
