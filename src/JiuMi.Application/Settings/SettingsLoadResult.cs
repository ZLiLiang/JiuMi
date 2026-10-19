namespace JiuMi.Application.Settings;

/// <summary>
/// The settings read at startup, the location they came from and any problems found.
/// </summary>
/// <param name="Settings">The loaded settings, with defaults filled in for missing keys.</param>
/// <param name="Location">The location this session reads from and writes to.</param>
/// <param name="Issues">The problems worth reporting to the user.</param>
/// <param name="Error">The exception behind a corrupt or unreadable file, when one occurred.</param>
public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLocation Location,
    SettingsLoadIssue Issues,
    Exception? Error = null);
