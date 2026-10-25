namespace JiuMi.Application.Settings;

/// <summary>
/// Decides, once at startup, which settings location this session reads from and writes to.
/// </summary>
public interface ISettingsLocationProvider
{
    /// <summary>
    /// Probes the executable-side directory first, then the per-user directory.
    /// </summary>
    /// <returns>The first writable location, or the memory-only location when neither is writable.</returns>
    SettingsLocation Locate();
}
