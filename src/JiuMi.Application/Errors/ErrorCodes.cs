namespace JiuMi.Application.Errors;

/// <summary>
/// The stable event codes used by the shell.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The clipboard could not be written; recorded for diagnostics only.</summary>
    public const string UnexpectedCopyFailed = "Unexpected.CopyFailed";

    /// <summary>The settings file could not be written.</summary>
    public const string SettingsSaveFailed = "Settings.SaveFailed";

    /// <summary>The settings file was invalid and was replaced with defaults.</summary>
    public const string SettingsCorrupted = "Settings.Corrupted";

    /// <summary>The requested display language has no usable text and was not applied.</summary>
    public const string LocalizationSwitchFailed = "Localization.SwitchFailed";

    /// <summary>The requested theme could not be applied and the previous one was kept.</summary>
    public const string ThemeSwitchFailed = "Theme.SwitchFailed";

    /// <summary>The executable-side settings directory was not writable.</summary>
    public const string ConfigFallbackToAppData = "Config.FallbackToAppData";

    /// <summary>No settings location is writable, so changes last for this session only.</summary>
    public const string ConfigMemoryOnly = "Config.MemoryOnly";

    /// <summary>An exception escaped the UI thread dispatcher.</summary>
    public const string UnexpectedUiThreadException = "Unexpected.UiThreadException";

    /// <summary>A task failed after nobody awaited it; recorded for diagnostics only.</summary>
    public const string UnexpectedUnobservedTask = "Unexpected.UnobservedTask";

    /// <summary>An exception reached the process-wide handler; recorded for diagnostics only.</summary>
    public const string UnexpectedAppDomainException = "Unexpected.AppDomainException";

    /// <summary>The error window could not be created or shown; recorded for diagnostics only.</summary>
    public const string UnexpectedErrorWindowFailed = "Unexpected.ErrorWindowFailed";
}
