namespace JiuMi.Application.Settings;

/// <summary>
/// Where the settings file of this session lives.
/// </summary>
public enum SettingsLocationKind
{
    /// <summary>The writable <c>config\</c> directory beside the executable.</summary>
    ExecutableDirectory,

    /// <summary>The per-user <c>%AppData%\JiuMi\</c> directory.</summary>
    AppData,

    /// <summary>No location is writable; settings live in memory for this session only.</summary>
    MemoryOnly,
}

/// <summary>
/// The settings location decided by the startup probe.
/// </summary>
/// <param name="Kind">Which of the candidate locations was selected.</param>
/// <param name="DirectoryPath">The directory holding the settings file, or <see langword="null"/> in memory-only mode.</param>
/// <param name="FilePath">The settings file path, or <see langword="null"/> in memory-only mode.</param>
public sealed record SettingsLocation(
    SettingsLocationKind Kind,
    string? DirectoryPath,
    string? FilePath)
{
    /// <summary>
    /// Gets the location used when neither candidate directory is writable.
    /// </summary>
    public static SettingsLocation MemoryOnly
    {
        get;
    } = new(SettingsLocationKind.MemoryOnly, null, null);

    /// <summary>
    /// Gets a value indicating whether settings can be written to disk.
    /// </summary>
    public bool IsPersistent => Kind != SettingsLocationKind.MemoryOnly;

    /// <summary>
    /// Creates a file-backed location inside <paramref name="directoryPath"/>.
    /// </summary>
    /// <param name="kind">Which candidate location this is.</param>
    /// <param name="directoryPath">The writable directory holding the settings file.</param>
    /// <returns>The location pointing at the settings file in that directory.</returns>
    public static SettingsLocation ForDirectory(SettingsLocationKind kind, string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return new(
            kind,
            directoryPath,
            Path.Combine(directoryPath, SettingsContract.FileName));
    }
}
