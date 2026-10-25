namespace JiuMi.Application.Settings;

/// <summary>
/// Applies a theme preference to the running interface.
/// </summary>
public interface IThemeApplier
{
    /// <summary>
    /// Applies <paramref name="theme"/> immediately.
    /// </summary>
    /// <param name="theme">The preference to apply.</param>
    /// <returns>
    /// <see langword="true"/> when the interface now uses <paramref name="theme"/>; <see langword="false"/>
    /// when its palette is unavailable and nothing was changed.
    /// </returns>
    bool TryApply(AppTheme theme);
}
