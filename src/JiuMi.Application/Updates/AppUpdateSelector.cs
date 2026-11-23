namespace JiuMi.Application.Updates;

/// <summary>
/// Selects the highest eligible update across all reachable sources for the installed version.
/// </summary>
public static class AppUpdateSelector
{
    /// <summary>
    /// Selects the highest release strictly newer than <paramref name="installedVersion"/>.
    /// </summary>
    /// <remarks>
    /// Stable installations only consider <see cref="AppUpdateManifest.Stable"/>; prerelease
    /// installations consider both <see cref="AppUpdateManifest.Stable"/> and
    /// <see cref="AppUpdateManifest.Preview"/>. When multiple sources report the same highest
    /// version, the earlier source in <paramref name="manifests"/> wins.
    /// </remarks>
    /// <param name="installedVersion">The semantic version of the running application.</param>
    /// <param name="manifests">Reachable source manifests in priority order.</param>
    /// <returns>The best update candidate, or <see langword="null"/> when already up to date.</returns>
    public static AppUpdateCandidate? SelectBest(
        SemanticVersion installedVersion,
        IReadOnlyList<AppUpdateSourceManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(installedVersion);
        ArgumentNullException.ThrowIfNull(manifests);

        AppUpdateCandidate? best = null;
        foreach (AppUpdateSourceManifest entry in manifests)
        {
            Consider(entry.Source, entry.Manifest.Stable, allowPrerelease: false);
            if (installedVersion.IsPrerelease)
            {
                Consider(entry.Source, entry.Manifest.Preview, allowPrerelease: true);
            }
        }

        return best;

        void Consider(AppUpdateSource source, AppUpdateRelease? release, bool allowPrerelease)
        {
            if (release is null)
            {
                return;
            }

            if (!allowPrerelease && (release.IsPrerelease || release.Version.IsPrerelease))
            {
                return;
            }

            if (release.Version <= installedVersion)
            {
                return;
            }

            if (best is null || release.Version > best.Release.Version)
            {
                best = new AppUpdateCandidate(release, source);
            }
        }
    }
}
