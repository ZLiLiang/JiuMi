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
    /// <param name="failure">The theme host failure when one occurred; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the interface now uses <paramref name="theme"/>; <see langword="false"/>
    /// when its palette is unavailable or the theme host failed, and nothing was changed.
    /// </returns>
    bool TryApply(AppTheme theme, out Exception? failure);
}
