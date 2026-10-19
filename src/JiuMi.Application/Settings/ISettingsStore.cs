namespace JiuMi.Application.Settings;

/// <summary>
/// Reads and writes the settings file at the location the startup probe selected.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Gets the location this session reads from and writes to.
    /// </summary>
    SettingsLocation Location
    {
        get;
    }

    /// <summary>
    /// Gets the settings as last loaded or saved in this session, including changes that failed to persist.
    /// </summary>
    AppSettings Current
    {
        get;
    }

    /// <summary>
    /// Loads the settings once at startup, backing up a corrupt file and filling in defaults.
    /// Repeated calls return the startup result.
    /// </summary>
    /// <returns>The loaded settings, the location and the problems found.</returns>
    SettingsLoadResult Load();

    /// <summary>
    /// Saves the whole settings file with a temporary file and an atomic replace.
    /// </summary>
    /// <param name="settings">The settings to write.</param>
    /// <returns>The save outcome; never throws to the caller.</returns>
    SettingsSaveResult Save(AppSettings settings);
}
