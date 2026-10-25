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
    /// Applies the preference stored in the settings, falling back to Fluent's light palette when it
    /// cannot be applied.
    /// </summary>
    void ApplyStoredTheme();

    /// <summary>
    /// Applies a theme preference and switches the interface immediately.
    /// </summary>
    /// <param name="theme">The requested preference.</param>
    /// <param name="failure">The theme host failure when one occurred; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the interface now uses <paramref name="theme"/>; <see langword="false"/>
    /// when the switch failed, in which case the previous preference is restored when possible.
    /// </returns>
    bool TrySetTheme(AppTheme theme, out Exception? failure);
}
