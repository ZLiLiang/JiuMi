using System.Threading;
using System.Threading.Tasks;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Supplies the world clock catalog. The implementation prefers a downloaded version that passed
/// verification and falls back to the bundled files, so the page never deals with the difference.
/// </summary>
public interface IWorldClockCatalogProvider
{
    /// <summary>
    /// Loads the catalog.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The catalog.</returns>
    Task<WorldClockCatalog> LoadAsync(CancellationToken cancellationToken = default);
}
