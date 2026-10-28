namespace JiuMi.Application.WorldClock;

/// <summary>
/// Compares catalog versions. A bundled catalog is version zero, so any published version replaces it.
/// </summary>
public static class WorldClockCatalogVersion
{
    /// <summary>
    /// Gets the version of the catalog shipped with the application.
    /// </summary>
    public const int Bundled = 0;

    /// <summary>
    /// Answers whether a published version replaces the installed one.
    /// </summary>
    /// <param name="candidate">The version the manifest publishes.</param>
    /// <param name="installed">The version in use; zero for the bundled catalog.</param>
    /// <returns><see langword="true"/> when the candidate is higher.</returns>
    public static bool IsNewer(int candidate, int installed) => candidate > installed;
}
