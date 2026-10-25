using JiuMi.Views;

namespace JiuMi.Startup;

/// <summary>
/// The startup work the splash covers, in the order the architecture fixes it.
/// </summary>
public sealed class ShellInitialization
{
    private readonly Action[] _steps;
    private readonly Func<MainWindow> _createMainWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellInitialization"/> class.
    /// </summary>
    /// <param name="steps">The work in the order it must happen: config probe, settings load, localization, theme.</param>
    /// <param name="createMainWindow">Builds the shell window, which has to come after every step.</param>
    public ShellInitialization(IEnumerable<Action> steps, Func<MainWindow> createMainWindow)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(createMainWindow);

        _steps = [.. steps];
        _createMainWindow = createMainWindow;
    }

    /// <summary>
    /// Runs the steps in order, then constructs the shell window.
    /// </summary>
    /// <returns>The constructed shell window, ready to hand over to.</returns>
    public MainWindow Run()
    {
        foreach (Action step in _steps)
        {
            step();
        }

        return _createMainWindow();
    }
}
