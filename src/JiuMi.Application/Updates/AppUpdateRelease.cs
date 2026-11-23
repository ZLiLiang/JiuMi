namespace JiuMi.Application.Updates;

/// <summary>
/// A single published release entry inside an update manifest.
/// </summary>
/// <param name="Version">The semantic version of the release.</param>
/// <param name="IsPrerelease">Whether the release is a preview/prerelease build.</param>
/// <param name="PublishedAt">When the release was published.</param>
/// <param name="PageUrl">The HTTPS release page opened when the user chooses to download.</param>
/// <param name="AssetUrl">Optional HTTPS direct asset URL reserved for future auto-update.</param>
/// <param name="AssetSha256">Optional SHA-256 hex digest reserved for future auto-update.</param>
/// <param name="AssetSize">Asset size in bytes reserved for future auto-update.</param>
public sealed record AppUpdateRelease(
    SemanticVersion Version,
    bool IsPrerelease,
    DateTimeOffset PublishedAt,
    Uri PageUrl,
    Uri? AssetUrl,
    string? AssetSha256,
    long AssetSize);
