namespace JiuMi.Application.Errors;

/// <summary>
/// The stable event codes registered by the shell.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The settings file could not be written.</summary>
    public const string SettingsSaveFailed = "Settings.SaveFailed";

    /// <summary>The settings file was invalid and was replaced with defaults.</summary>
    public const string SettingsCorrupted = "Settings.Corrupted";

    /// <summary>The requested display language has no usable text and was not applied.</summary>
    public const string LocalizationSwitchFailed = "Localization.SwitchFailed";

    /// <summary>The executable-side settings directory was not writable.</summary>
    public const string ConfigFallbackToAppData = "Config.FallbackToAppData";

    /// <summary>No settings location is writable, so changes last for this session only.</summary>
    public const string ConfigMemoryOnly = "Config.MemoryOnly";
}
