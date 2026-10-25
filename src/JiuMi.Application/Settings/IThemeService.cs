namespace JiuMi.Application.Settings;

/// <summary>
/// Applies the theme preference to the running interface.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the preference currently applied.
    /// </summary>
    AppTheme Current
    {
        get;
    }

    /// <summary>
    /// Applies the preference stored in the settings, falling back to the Fluent default theme when it
    /// cannot be applied.
    /// </summary>
    void ApplyStoredTheme();

    /// <summary>
    /// Applies a theme preference and switches the interface immediately.
    /// </summary>
    /// <param name="theme">The requested preference.</param>
    /// <returns>
    /// <see langword="true"/> when the interface now uses <paramref name="theme"/>; <see langword="false"/>
    /// when the switch failed, in which case the previous preference is restored when possible.
    /// </returns>
    bool TrySetTheme(AppTheme theme);
}
