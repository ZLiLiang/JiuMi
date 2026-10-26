using JiuMi.Application.Settings;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Resolves the log directory that belongs beside the effective settings location.
/// </summary>
internal static class LogPaths
{
    private const string DirectoryName = "logs";

    /// <summary>
    /// Gets the log directory for a settings location, or <see langword="null"/> in memory-only mode.
    /// </summary>
    /// <param name="location">The location the startup probe settled on.</param>
    /// <returns>The directory logs are written to, or <see langword="null"/> when nothing is writable.</returns>
    public static string? For(SettingsLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.Kind switch
        {
            // The executable location points at <exe>\config; logs are its sibling, not its child.
            SettingsLocationKind.ExecutableDirectory => SiblingOfConfigDirectory(location.DirectoryPath),
            SettingsLocationKind.AppData => location.DirectoryPath is null
                ? null
                : Path.Combine(location.DirectoryPath, DirectoryName),
            _ => null,
        };
    }

    private static string? SiblingOfConfigDirectory(string? configDirectory)
    {
        if (configDirectory is null)
        {
            return null;
        }

        string fullPath = Path.GetFullPath(configDirectory);
        string? parent = Path.GetDirectoryName(fullPath);
        return Path.Combine(parent ?? fullPath, DirectoryName);
    }
}
