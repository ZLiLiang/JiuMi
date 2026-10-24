namespace JiuMi.Application.Errors;

/// <summary>
/// Maps each registered event code to the surface that presents it.
/// </summary>
public static class ErrorPresentationCatalog
{
    private static readonly Dictionary<string, ErrorPresentation> _presentations = new(StringComparer.Ordinal)
    {
        [ErrorCodes.SettingsSaveFailed] = new(
            ErrorLevel.Error,
            ErrorCarrier.InfoBar,
            ErrorNoticeAction.Retry),
        [ErrorCodes.SettingsCorrupted] = new(ErrorLevel.Warning, ErrorCarrier.InfoBar),
        [ErrorCodes.LocalizationSwitchFailed] = new(ErrorLevel.Warning, ErrorCarrier.InfoBar),
        [ErrorCodes.ConfigFallbackToAppData] = new(
            ErrorLevel.Warning,
            ErrorCarrier.InfoBar,
            ErrorNoticeAction.OpenFolder),
        [ErrorCodes.ConfigMemoryOnly] = new(ErrorLevel.Error, ErrorCarrier.InfoBar, IsPersistent: true),
    };

    /// <summary>
    /// Gets the event codes registered for presentation.
    /// </summary>
    public static IReadOnlyCollection<string> Codes => _presentations.Keys.ToArray();

    /// <summary>
    /// Finds the presentation for an event code.
    /// </summary>
    /// <param name="code">The stable event code.</param>
    /// <returns>The presentation, or <see langword="null"/> when the code is not registered.</returns>
    public static ErrorPresentation? Find(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return _presentations.TryGetValue(code, out ErrorPresentation? presentation) ? presentation : null;
    }
}
