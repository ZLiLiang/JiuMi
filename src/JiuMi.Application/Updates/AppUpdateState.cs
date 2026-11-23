namespace JiuMi.Application.Updates;

/// <summary>
/// Persisted update check state stored in <c>updates/state.json</c>.
/// </summary>
/// <param name="LastCheckedAt">When the last successful update check completed, or <see langword="null"/>.</param>
/// <param name="LastNotifiedVersion">The canonical semantic version for which a tray notification was last shown.</param>
public sealed record AppUpdateState(
    DateTimeOffset? LastCheckedAt,
    string? LastNotifiedVersion);
