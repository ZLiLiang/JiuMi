namespace JiuMi.Application.Updates;

/// <summary>
/// The result returned by a single update check invocation.
/// </summary>
/// <param name="Status">The outcome of the check.</param>
/// <param name="AvailableUpdate">The newer release when <paramref name="Status"/> is <see cref="AppUpdateCheckStatus.UpdateAvailable"/>.</param>
/// <param name="ShouldNotify">Whether a background tray notification should be shown for <paramref name="AvailableUpdate"/>.</param>
/// <param name="LastCheckedAt">The last successful check time after this call.</param>
public sealed record AppUpdateCheckResult(
    AppUpdateCheckStatus Status,
    AppUpdateCandidate? AvailableUpdate = null,
    bool ShouldNotify = false,
    DateTimeOffset? LastCheckedAt = null);
