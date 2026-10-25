using Avalonia.Controls;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using JiuMi.Application.Errors;
using JiuMi.Application.Settings;

namespace JiuMi.Theming;

/// <summary>
/// Applies theme preferences through the running Avalonia application.
/// </summary>
public sealed class AvaloniaThemeApplier : IThemeApplier
{
    private const string ProbeResourceKey = "SolidBackgroundFillColorBaseBrush";

    private readonly Func<global::Avalonia.Application?> _applicationAccessor;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaThemeApplier"/> class.
    /// </summary>
    /// <param name="applicationAccessor">Returns the running application, or <see langword="null"/> before it exists.</param>
    /// <param name="errorReporter">Receives diagnostic records when a preference cannot be applied.</param>
    /// <param name="recordFactory">Creates the diagnostic records.</param>
    public AvaloniaThemeApplier(
        Func<global::Avalonia.Application?> applicationAccessor,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory)
    {
        ArgumentNullException.ThrowIfNull(applicationAccessor);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        _applicationAccessor = applicationAccessor;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
    }

    /// <inheritdoc />
    public bool TryApply(AppTheme theme)
    {
        if (_applicationAccessor() is not { } application)
        {
            ReportApplyFailure();
            return false;
        }

        try
        {
            FluentAvaloniaTheme? fluentTheme = application.Styles
                .OfType<FluentAvaloniaTheme>()
                .FirstOrDefault();
            if (fluentTheme is null || !HasPalette(fluentTheme, theme))
            {
                // Without the palette the requested theme would render with the default one.
                ReportApplyFailure();
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
        catch (Exception error)
        {
            // The theme host is a UI framework boundary; record the failure instead of crashing.
            ReportApplyFailure(error);
            return false;
        }
    }

    private static bool HasPalette(FluentAvaloniaTheme theme, AppTheme preference)
    {
        // "Default" resolves against the operating system at lookup time, so there is no palette to probe.
        if (preference == AppTheme.System)
        {
            return true;
        }

        if (preference == AppTheme.Dark)
        {
            return HasVariantPalette(theme.Resources, ThemeVariant.Dark);
        }

        // Fluent keys its light palette as "Default"; a dedicated "Light" dictionary is optional.
        return HasVariantPalette(theme.Resources, ThemeVariant.Light) ||
            HasVariantPalette(theme.Resources, ThemeVariant.Default);
    }

    private static bool HasVariantPalette(IResourceProvider resources, ThemeVariant variant)
    {
        if (resources is not IResourceDictionary dictionary)
        {
            return false;
        }

        // A themed lookup through the resource host falls back to the default dictionary, which hides a
        // missing variant. Look for the variant's own palette instead, including in merged dictionaries.
        if (dictionary.ThemeDictionaries.TryGetValue(variant, out IThemeVariantProvider? palette) &&
            palette.TryGetResource(ProbeResourceKey, theme: null, out _))
        {
            return true;
        }

        foreach (IResourceProvider merged in dictionary.MergedDictionaries)
        {
            if (HasVariantPalette(merged, variant))
            {
                return true;
            }
        }

        return false;
    }

    private void ReportApplyFailure(Exception? error = null) =>
        _errorReporter.Report(_recordFactory.Create(
            ErrorCodes.ThemeApplyFailed,
            "Theme.Apply",
            error,
            ErrorLevel.Debug));
}
