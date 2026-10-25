using Avalonia.Controls.ApplicationLifetimes;

namespace JiuMi.Startup;

/// <summary>
/// Starts the application shell: the splash sequence and the work it covers.
/// </summary>
public interface IShellStartup
{
    /// <summary>
    /// Puts the startup sequence on screen and starts the work behind it.
    /// </summary>
    /// <param name="desktop">The lifetime that owns the window and the shutdown rule.</param>
    void Start(IClassicDesktopStyleApplicationLifetime desktop);
}
