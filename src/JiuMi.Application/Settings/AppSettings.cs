namespace JiuMi.Application.Settings;

/// <summary>
/// The user-adjustable state persisted in the settings file.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// Gets the schema version written with the settings.
    /// </summary>
    public int SchemaVersion
    {
        get;
        init;
    } = SettingsContract.SchemaVersion;

    /// <summary>
    /// Gets the concrete language code, or <see langword="null"/> when no language was stored yet.
    /// </summary>
    public string? Language
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the theme preference.
    /// </summary>
    public AppTheme Theme
    {
        get;
        init;
    } = AppTheme.System;

    /// <summary>
    /// Gets a value indicating whether the navigation rail is expanded.
    /// </summary>
    public bool NavExpanded
    {
        get;
        init;
    } = true;

    /// <summary>
    /// Creates the settings used when the file is missing or unusable.
    /// </summary>
    /// <returns>The default settings.</returns>
    public static AppSettings CreateDefault() => new();
}
