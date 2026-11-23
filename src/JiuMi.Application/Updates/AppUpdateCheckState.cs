namespace JiuMi.Application.Updates;

/// <summary>
/// The UI-facing state of the application update check shown in the About card.
/// </summary>
public enum AppUpdateCheckState
{
    /// <summary>
    /// No check has completed yet.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// An update check is currently in progress.
    /// </summary>
    Checking = 1,

    /// <summary>
    /// The last check confirmed the running version is up to date.
    /// </summary>
    UpToDate = 2,

    /// <summary>
    /// A newer version is available for download.
    /// </summary>
    UpdateAvailable = 3,

    /// <summary>
    /// The last manual check failed on all sources.
    /// </summary>
    Failed = 4,
}
