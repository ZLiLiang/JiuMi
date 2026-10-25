using Avalonia.Controls;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using JiuMi.Application.Settings;

namespace JiuMi.Theming;

/// <summary>
/// Detects whether the Fluent theme ships the palette a preference needs.
/// </summary>
internal static class FluentThemePalette
{
    /// <summary>
    /// Returns whether <paramref name="preference"/> has its own palette in <paramref name="theme"/>.
    /// </summary>
    /// <param name="theme">The Fluent theme whose resources are inspected.</param>
    /// <param name="preference">The theme preference to check.</param>
    /// <returns><see langword="true"/> when applying the preference cannot fall back to another palette.</returns>
    public static bool Has(FluentAvaloniaTheme theme, AppTheme preference)
    {
        ArgumentNullException.ThrowIfNull(theme);

        // "Default" resolves against the operating system at lookup time, so there is no palette to probe.
        if (preference == AppTheme.System)
        {
            return true;
        }

        if (preference == AppTheme.Dark)
        {
            return HasVariant(theme.Resources, ThemeVariant.Dark);
        }

        // Fluent keys its light palette as "Default"; a dedicated "Light" dictionary is optional.
        return HasVariant(theme.Resources, ThemeVariant.Light) ||
            HasVariant(theme.Resources, ThemeVariant.Default);
    }

    private static bool HasVariant(IResourceProvider resources, ThemeVariant variant)
    {
        if (resources is not IResourceDictionary dictionary)
        {
            return false;
        }

        // A themed lookup through the resource host falls back to the default dictionary, which hides a
        // missing variant. Look for the variant's own palette instead, including in merged dictionaries.
        if (dictionary.ThemeDictionaries.TryGetValue(variant, out IThemeVariantProvider? palette) &&
            palette.TryGetResource(ThemeResourceKeys.SolidBackgroundBrush, theme: null, out _))
        {
            return true;
        }

        foreach (IResourceProvider merged in dictionary.MergedDictionaries)
        {
            if (HasVariant(merged, variant))
            {
                return true;
            }
        }

        return false;
    }
}
