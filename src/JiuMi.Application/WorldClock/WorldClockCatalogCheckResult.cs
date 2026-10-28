using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// What one catalog check did.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="InstalledVersion">The catalog version in use after the check; zero for the bundled catalog.</param>
/// <param name="UpdatedAt">When the installed catalog data changed, when known.</param>
/// <param name="Failure">The failure, when the check could not finish.</param>
public sealed record WorldClockCatalogCheckResult(
    WorldClockCatalogCheckStatus Status,
    int InstalledVersion,
    DateTimeOffset? UpdatedAt,
    Exception? Failure = null);
