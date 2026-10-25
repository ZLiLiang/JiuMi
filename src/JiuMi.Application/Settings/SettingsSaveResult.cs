namespace JiuMi.Application.Settings;

/// <summary>
/// The outcome of saving the settings.
/// </summary>
/// <param name="Persisted">Whether the settings reached the settings file.</param>
/// <param name="Error">
/// The failure to report when a write was attempted and failed. <see langword="null"/> when the write
/// succeeded, or when the session is memory-only and the permanent location warning already explains
/// why nothing was written.
/// </param>
public sealed record SettingsSaveResult(bool Persisted, Exception? Error = null)
{
    /// <summary>
    /// Gets the result of a write that reached the file.
    /// </summary>
    public static SettingsSaveResult Success
    {
        get;
    } = new(Persisted: true);

    /// <summary>
    /// Gets the result of a save in memory-only mode, where no file exists to write.
    /// </summary>
    public static SettingsSaveResult NotPersisted
    {
        get;
    } = new(Persisted: false);

    /// <summary>
    /// Creates the result of a write that was attempted and failed.
    /// </summary>
    /// <param name="error">The failure.</param>
    /// <returns>The failed result.</returns>
    public static SettingsSaveResult Failed(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(Persisted: false, error);
    }
}
