namespace JiuMi.Application.Updates;

/// <summary>
/// The outcome of a single <see cref="IAppUpdateChecker.CheckAsync"/> call.
/// </summary>
public enum AppUpdateCheckStatus
{
    /// <summary>
    /// Background checking is turned off via <c>OnlineDataEnabled</c>.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The check was skipped because the 24-hour interval had not elapsed or another check was running.
    /// </summary>
    Skipped = 1,

    /// <summary>
    /// At least one source responded and no newer eligible version was found.
    /// </summary>
    UpToDate = 2,

    /// <summary>
    /// At least one source responded with a newer eligible version.
    /// </summary>
    UpdateAvailable = 3,

    /// <summary>
    /// Every configured update source failed.
    /// </summary>
    Failed = 4,
}
