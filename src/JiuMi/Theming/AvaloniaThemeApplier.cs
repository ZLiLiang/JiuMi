using Avalonia.Styling;
using FluentAvalonia.Styling;
using JiuMi.Application.Settings;

namespace JiuMi.Theming;

/// <summary>
/// Applies theme preferences through the running Avalonia application.
/// </summary>
public sealed class AvaloniaThemeApplier : IThemeApplier
{
    private readonly Func<global::Avalonia.Application?> _applicationAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaThemeApplier"/> class.
    /// </summary>
    /// <param name="applicationAccessor">Returns the running application, or <see langword="null"/> before it exists.</param>
    public AvaloniaThemeApplier(Func<global::Avalonia.Application?> applicationAccessor)
    {
        ArgumentNullException.ThrowIfNull(applicationAccessor);
        _applicationAccessor = applicationAccessor;
    }

    /// <inheritdoc />
    public bool TryApply(AppTheme theme)
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
            if (fluentTheme is null || !FluentThemePalette.Has(fluentTheme, theme))
            {
                // Without the palette the requested theme would render with the default one.
                return false;
            }

            // Explicit choices must stop the Fluent theme from following later OS changes.
            fluentTheme.PreferSystemTheme = theme == AppTheme.System;
            application.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
            return true;
        }
        catch (Exception)
        {
            // The theme host is a UI framework boundary; fall back instead of crashing.
            return false;
        }
    }
}
