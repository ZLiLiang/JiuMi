namespace JiuMi.Application.Updates;

/// <summary>
/// Checks the published GitHub and Gitee release manifests for newer application versions.
/// </summary>
public interface IAppUpdateChecker
{
    /// <summary>
    /// Gets the current UI-facing update status.
    /// </summary>
    AppUpdateStatus Status
    {
        get;
    }

    /// <summary>
    /// Raised whenever <see cref="Status"/> changes.
    /// </summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Checks for a newer version of the application.
    /// </summary>
    /// <param name="isManual">
    /// <see langword="true"/> when triggered by the user from the About card (bypasses
    /// <c>OnlineDataEnabled</c> and the 24-hour throttle, never requests a tray notification,
    /// and surfaces failure in <see cref="Status"/>); <see langword="false"/> for automatic
    /// background checks.
    /// </param>
    /// <param name="cancellationToken">Cancels the in-flight HTTP requests.</param>
    /// <returns>The outcome of the check.</returns>
    Task<AppUpdateCheckResult> CheckAsync(bool isManual, CancellationToken cancellationToken = default);
}
