using Avalonia.Styling;
using FluentAvalonia.Styling;
using JiuMi.Application.Settings;

namespace JiuMi.Theming;

/// <summary>
/// Applies the theme preference through the running Avalonia application.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string ProbeResourceKey = "SolidBackgroundFillColorBaseBrush";

    private readonly Func<global::Avalonia.Application?> _applicationAccessor;
    private readonly ISettingsStore _settingsStore;
    private AppTheme _current = AppTheme.System;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies the stored preference at startup.</param>
    /// <param name="applicationAccessor">Returns the running application, or <see langword="null"/> before it exists.</param>
    public ThemeService(
        ISettingsStore settingsStore,
        Func<global::Avalonia.Application?> applicationAccessor)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(applicationAccessor);
        _settingsStore = settingsStore;
        _applicationAccessor = applicationAccessor;
    }

    /// <inheritdoc />
    public AppTheme Current => _current;

    /// <inheritdoc />
    public void ApplyStoredTheme()
    {
        AppTheme stored = _settingsStore.Current.Theme;
        if (TryApply(stored))
        {
            _current = stored;
            return;
        }

        FallBackToSystem();
    }

    /// <inheritdoc />
    public bool TrySetTheme(AppTheme theme)
    {
        if (theme == _current)
        {
            return true;
        }

        if (TryApply(theme))
        {
            _current = theme;
            return true;
        }

        if (!TryApply(_current))
        {
            FallBackToSystem();
        }

        return false;
    }

    private bool TryApply(AppTheme theme)
    {
        if (_applicationAccessor() is not { } application)
        {
            return false;
        }

        try
        {
            FluentAvaloniaTheme? fluentTheme = application.Styles
                .OfType<FluentAvaloniaTheme>()
                .FirstOrDefault();
            if (fluentTheme is not null)
            {
                // Explicit choices must stop the Fluent theme from following later OS changes.
                fluentTheme.PreferSystemTheme = theme == AppTheme.System;
            }

            application.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };

            // Without the palette the shell has no readable surface; the caller reports the failure.
            if (application.TryGetResource(ProbeResourceKey, application.ActualThemeVariant, out _))
            {
                return true;
            }

            application.RequestedThemeVariant = ThemeVariant.Default;
            return false;
        }
        catch (Exception)
        {
            // The theme host is a UI framework boundary; report the failure instead of crashing.
            return false;
        }
    }

    private void FallBackToSystem()
    {
        _current = AppTheme.System;
        _ = TryApply(_current);
    }
}
