namespace JiuMi.Application.Updates;

/// <summary>
/// The two-channel update manifest published under the <c>app-update</c> release tag.
/// </summary>
/// <param name="GeneratedAt">When the manifest was generated.</param>
/// <param name="Stable">The latest stable release on this host, or <see langword="null"/>.</param>
/// <param name="Preview">The latest prerelease on this host, or <see langword="null"/>.</param>
public sealed record AppUpdateManifest(
    DateTimeOffset GeneratedAt,
    AppUpdateRelease? Stable,
    AppUpdateRelease? Preview);
