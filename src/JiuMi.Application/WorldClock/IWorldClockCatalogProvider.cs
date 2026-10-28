using System.Threading;
using System.Threading.Tasks;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Supplies the world clock catalog. The bundled implementation reads the package files; a future
/// online implementation can replace it without changing the page.
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
