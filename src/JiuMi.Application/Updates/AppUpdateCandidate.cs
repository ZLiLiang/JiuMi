namespace JiuMi.Application.Updates;

/// <summary>
/// A newer release selected for the running installation together with the host that serves it.
/// </summary>
/// <param name="Release">The release metadata, including its download page URL.</param>
/// <param name="Source">The host from which this release was selected.</param>
public sealed record AppUpdateCandidate(
    AppUpdateRelease Release,
    AppUpdateSource Source);
