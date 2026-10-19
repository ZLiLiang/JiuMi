namespace JiuMi.Application.Settings;

/// <summary>
/// The names and version shared with the on-disk settings file.
/// </summary>
public static class SettingsContract
{
    /// <summary>
    /// The schema version written on every save.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// The settings file name inside the effective settings directory.
    /// </summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// The temporary file written beside the settings file before the atomic replace.
    /// </summary>
    public const string TempFileName = "settings.json.tmp";

    /// <summary>
    /// The single backup that receives a corrupt settings file.
    /// </summary>
    public const string BackupFileName = "settings.json.bak";
}
