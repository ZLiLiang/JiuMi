namespace JiuMi.Application.Updates;

/// <summary>
/// A snapshot of the application update checker state for the About card.
/// </summary>
/// <param name="State">The current check state.</param>
/// <param name="LastCheckedAt">When the last successful check completed, or <see langword="null"/>.</param>
/// <param name="AvailableUpdate">The selected newer release when <paramref name="State"/> is <see cref="AppUpdateCheckState.UpdateAvailable"/>.</param>
public sealed record AppUpdateStatus(
    AppUpdateCheckState State,
    DateTimeOffset? LastCheckedAt,
    AppUpdateCandidate? AvailableUpdate)
{
    /// <summary>
    /// Gets a value indicating whether an update check is currently running.
    /// </summary>
    public bool IsChecking => State == AppUpdateCheckState.Checking;
}
