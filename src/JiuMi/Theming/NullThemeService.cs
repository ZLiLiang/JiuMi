using JiuMi.Application.Settings;

namespace JiuMi.Theming;

/// <summary>
/// Leaves the default theme in place; used when the application is created by the XAML runtime.
/// </summary>
internal sealed class NullThemeService : IThemeService
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static NullThemeService Instance { get; } = new();

    private NullThemeService()
    {
    }

    /// <inheritdoc />
    /// <remarks>Only the default theme is in effect; explicit choices have no host to apply them.</remarks>
    public AppTheme Current => AppTheme.System;

    /// <inheritdoc />
    public void ApplyStoredTheme()
    {
    }

    /// <inheritdoc />
    public bool TrySetTheme(AppTheme theme) => theme == AppTheme.System;
}
