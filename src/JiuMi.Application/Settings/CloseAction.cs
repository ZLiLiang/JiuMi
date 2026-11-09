namespace JiuMi.Application.Settings;

/// <summary>
/// The behavior the main window close button follows.
/// </summary>
public enum CloseAction
{
    /// <summary>Ask every time the main window is closed.</summary>
    Ask,

    /// <summary>Hide the main window to the tray and keep the process running.</summary>
    MinimizeToTray,

    /// <summary>Exit the process.</summary>
    Exit,
}
