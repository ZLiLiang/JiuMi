namespace JiuMi.Application.Settings;

/// <summary>
/// Problems found while loading the settings that the shell must surface to the user.
/// </summary>
[Flags]
public enum SettingsLoadIssue
{
    /// <summary>No problem was found.</summary>
    None = 0,

    /// <summary>The file was not valid settings JSON; it was renamed to the backup file.</summary>
    Corrupted = 1 << 0,

    /// <summary>The executable-side directory was not usable, so the per-user directory was chosen.</summary>
    FallbackToAppData = 1 << 1,

    /// <summary>Neither directory was writable, so settings live in memory for this session.</summary>
    MemoryOnly = 1 << 2,
}
