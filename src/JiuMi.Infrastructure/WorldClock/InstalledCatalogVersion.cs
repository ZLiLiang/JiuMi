using System;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// A catalog version that is cached and verified, with the date its data changed.
/// </summary>
/// <param name="Version">The catalog version.</param>
/// <param name="UpdatedAt">When the catalog data changed.</param>
internal sealed record InstalledCatalogVersion(int Version, DateTimeOffset UpdatedAt);
