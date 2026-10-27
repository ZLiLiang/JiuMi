namespace JiuMi.Application.Shell;

/// <summary>
/// The notification-area entry: it brings the window forward and offers the fixed three-item menu.
/// </summary>
/// <remarks>
/// The interface is the whole platform story the shell knows. The Windows implementation lives in the
/// infrastructure layer; other platforms report that they cannot show the icon.
/// </remarks>
public interface ITrayIcon : IDisposable
{
    /// <summary>
    /// Raised when the icon is clicked and the window should come forward.
    /// </summary>
    event EventHandler? ShowRequested;

    /// <summary>
    /// Raised when the settings item of the menu is chosen.
    /// </summary>
    event EventHandler? SettingsRequested;

    /// <summary>
    /// Raised when the exit item of the menu is chosen.
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Shows the icon, or refreshes its tooltip and menu labels when it is already visible.
    /// </summary>
    /// <param name="toolTip">The hover text, which is the application name.</param>
    /// <param name="menu">The localized menu labels.</param>
    /// <returns><see langword="true"/> when the platform shows the icon.</returns>
    bool Show(string toolTip, TrayMenuText menu);
}
