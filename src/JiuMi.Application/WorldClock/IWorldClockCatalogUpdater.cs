using System;
using System.Threading;
using System.Threading.Tasks;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Checks the published catalog, downloads a newer version and caches it for the next page load.
/// </summary>
/// <remarks>
/// Failures are silent: the installed catalog stays in use and the implementation only logs a warning.
/// </remarks>
public interface IWorldClockCatalogUpdater
{
    /// <summary>
    /// Gets the current update state for display.
    /// </summary>
    WorldClockCatalogUpdateStatus Status
    {
        get;
    }

    /// <summary>
    /// Raised when <see cref="Status"/> changes; may run on any thread.
    /// </summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Checks the published catalog once.
    /// </summary>
    /// <param name="force">Ignores the daily interval; the data-and-connection switch still applies.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>What the check did; never throws for network or validation failures.</returns>
    Task<WorldClockCatalogCheckResult> CheckAsync(
        bool force,
        CancellationToken cancellationToken = default);
}
