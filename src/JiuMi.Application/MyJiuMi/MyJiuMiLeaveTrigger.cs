namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The reason the shell is leaving the screen, used to decide whether the cat walks out.
/// </summary>
public enum MyJiuMiLeaveTrigger
{
    /// <summary>
    /// The user chose to hide the main window to the tray.
    /// </summary>
    HiddenToTray,

    /// <summary>
    /// The main window was minimized.
    /// </summary>
    Minimized,

    /// <summary>
    /// The user dragged the cat past the room edge and released it outside.
    /// </summary>
    DraggedOut,

    /// <summary>
    /// The application is exiting; the window is not hidden and kept.
    /// </summary>
    Exiting,

    /// <summary>
    /// The user navigated away from the My JiuMi page.
    /// </summary>
    PageLeft,
}
