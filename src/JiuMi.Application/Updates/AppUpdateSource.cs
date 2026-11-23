namespace JiuMi.Application.Updates;

/// <summary>
/// A release host that serves an <c>update.json</c> manifest.
/// </summary>
/// <param name="Name">Human-readable host identifier, such as <c>Gitee</c> or <c>GitHub</c>.</param>
/// <param name="RepositoryUrl">The HTTPS repository root URL on this host.</param>
/// <param name="ManifestUri">The HTTPS URI of the <c>app-update/update.json</c> asset.</param>
public sealed record AppUpdateSource(
    string Name,
    string RepositoryUrl,
    Uri ManifestUri);
