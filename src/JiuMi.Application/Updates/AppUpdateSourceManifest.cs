namespace JiuMi.Application.Updates;

/// <summary>
/// Pairs an update source with the manifest fetched from it.
/// </summary>
/// <param name="Source">The source that supplied the manifest.</param>
/// <param name="Manifest">The parsed manifest.</param>
public sealed record AppUpdateSourceManifest(
    AppUpdateSource Source,
    AppUpdateManifest Manifest);
