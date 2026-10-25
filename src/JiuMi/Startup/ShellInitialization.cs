using JiuMi.Views;

namespace JiuMi.Startup;

/// <summary>
/// The startup work the splash covers, in the order the architecture fixes it, and the shell window
/// construction that follows it.
/// </summary>
/// <remarks>
/// Steps run one after another but never on a fixed thread: each one queues its own I/O to the thread
/// pool and returns to the caller. The splash keeps drawing while they wait, which is what lets the
/// readiness timeout interrupt a slow startup. The shell window is built separately, because the
/// fallback may have to show it before every step has finished.
/// </remarks>
public sealed class ShellInitialization
{
    private readonly Func<Task>[] _steps;
    private readonly Func<MainWindow> _createMainWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellInitialization"/> class.
    /// </summary>
    /// <param name="steps">The work in the order it must happen: config probe, settings load, localization, theme.</param>
    /// <param name="createMainWindow">Builds the shell window, which normally comes after every step.</param>
    public ShellInitialization(IEnumerable<Func<Task>> steps, Func<MainWindow> createMainWindow)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(createMainWindow);

        _steps = [.. steps];
        _createMainWindow = createMainWindow;
    }

    /// <summary>
    /// Runs the steps in order.
    /// </summary>
    /// <returns>A task that completes when every step has finished.</returns>
    public async Task RunAsync()
    {
        foreach (Func<Task> step in _steps)
        {
            await step();
        }
    }

    /// <summary>
    /// Constructs the shell window.
    /// </summary>
    /// <returns>The constructed shell window, ready to hand over to.</returns>
    public MainWindow CreateMainWindow() => _createMainWindow();
}
