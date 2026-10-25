using Avalonia.Controls;
using Avalonia.Styling;
using JiuMi.Application.Settings;

namespace JiuMi.Theming;

/// <summary>
/// Detects whether the Fluent theme ships the palette a preference needs.
/// </summary>
internal static class FluentThemePalette
{
    private static readonly string[] _paletteKeys =
        [ThemeResourceKeys.SolidBackgroundBrush, ThemeResourceKeys.PrimaryTextBrush];

    /// <summary>
    /// Returns whether <paramref name="resources"/> has the palette for <paramref name="preference"/>.
    /// </summary>
    /// <param name="resources">The themed resource host to inspect.</param>
    /// <param name="preference">The theme preference to check.</param>
    /// <returns><see langword="true"/> when applying the preference has a complete palette to use.</returns>
    public static bool HasPaletteFor(IResourceProvider resources, AppTheme preference)
    {
        ArgumentNullException.ThrowIfNull(resources);

        // "Default" resolves against the operating system at lookup time, so there is no palette to probe.
        if (preference == AppTheme.System)
        {
            return true;
        }

        if (preference == AppTheme.Dark)
        {
            return HasVariant(resources, ThemeVariant.Dark);
        }

        // Fluent keys its light palette as "Default"; a dedicated "Light" dictionary is optional.
        return HasVariant(resources, ThemeVariant.Light) ||
            HasVariant(resources, ThemeVariant.Default);
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
            HasAllKeys(palette))
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

    private static bool HasAllKeys(IResourceProvider palette)
    {
        foreach (string key in _paletteKeys)
        {
            if (!palette.TryGetResource(key, theme: null, out _))
            {
                return false;
            }
        }

        return true;
    }
}
