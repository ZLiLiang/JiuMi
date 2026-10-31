using JiuMi.Application.FileRename;
using JiuMi.Application.ImageConversion;
using JiuMi.Application.WorldClock;

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
    /// Gets a value indicating whether the tools may fetch data from the network.
    /// </summary>
    public bool OnlineDataEnabled
    {
        get;
        init;
    } = true;

    /// <summary>
    /// Gets the persisted world clock view and places.
    /// </summary>
    public WorldClockSettings WorldClock
    {
        get;
        init;
    } = WorldClockSettings.CreateDefault();

    /// <summary>
    /// Gets the persisted image conversion parameters.
    /// </summary>
    public ImageConvertSettings ImageConvert
    {
        get;
        init;
    } = ImageConvertSettings.CreateDefault();

    /// <summary>
    /// Gets the persisted batch rename parameters.
    /// </summary>
    public FileRenameSettings FileRename
    {
        get;
        init;
    } = FileRenameSettings.CreateDefault();

    /// <summary>
    /// Creates the settings used when the file is missing or unusable.
    /// </summary>
    /// <returns>The default settings.</returns>
    public static AppSettings CreateDefault() => new();
}
