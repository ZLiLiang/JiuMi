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
    public bool TryApply(AppTheme theme, out Exception? failure)
    {
        failure = null;
        if (_applicationAccessor() is not { } application)
        {
            return false;
        }

        FluentAvaloniaTheme? fluentTheme = application.Styles
            .OfType<FluentAvaloniaTheme>()
            .FirstOrDefault();
        bool previousPreferSystemTheme = fluentTheme?.PreferSystemTheme ?? false;
        ThemeVariant? previousVariant = application.RequestedThemeVariant;
        try
        {
            if (fluentTheme is null || !FluentThemePalette.HasPaletteFor(fluentTheme.Resources, theme))
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
        catch (Exception exception)
        {
            // The theme host is a UI framework boundary; hand the failure back instead of crashing.
            // A half-applied host would contradict the contract, so put both properties back.
            TryRestore(fluentTheme, application, previousVariant, previousPreferSystemTheme);
            failure = exception;
            return false;
        }
    }

    private static void TryRestore(
        FluentAvaloniaTheme? fluentTheme,
        global::Avalonia.Application application,
        ThemeVariant? variant,
        bool preferSystemTheme)
    {
#pragma warning disable CA1031 // A host that failed once may fail again while restoring.
        try
        {
            if (fluentTheme is not null)
            {
                fluentTheme.PreferSystemTheme = preferSystemTheme;
            }

            application.RequestedThemeVariant = variant;
        }
        catch (Exception)
        {
            // Report the original failure rather than a secondary restore error.
        }
#pragma warning restore CA1031
    }
}
