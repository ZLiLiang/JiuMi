using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The catalog update state the settings page shows.
/// </summary>
/// <param name="IsChecking">Whether a check is running right now.</param>
/// <param name="LastCheckedAt">When the last automatic or manual check ran.</param>
/// <param name="InstalledVersion">The catalog version in use; zero for the bundled catalog.</param>
/// <param name="InstalledUpdatedAt">When the installed catalog data changed, when known.</param>
public sealed record WorldClockCatalogUpdateStatus(
    bool IsChecking,
    DateTimeOffset? LastCheckedAt,
    int InstalledVersion,
    DateTimeOffset? InstalledUpdatedAt)
{
    /// <summary>
    /// Gets a value indicating whether the catalog in use was downloaded rather than bundled.
    /// </summary>
    public bool HasInstalledUpdate => InstalledVersion > WorldClockCatalogVersion.Bundled;
}
