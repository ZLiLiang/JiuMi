using Avalonia.Controls;
using Avalonia.Styling;
using JiuMi.Application.Settings;
using JiuMi.Theming;

namespace JiuMi.Tests;

public sealed class FluentThemePaletteTests
{
    [Fact]
    public void Has_GivenDarkPalette_AcceptsDark()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Dark] = CreatePalette();

        Assert.True(FluentThemePalette.Has(resources, AppTheme.Dark));
    }

    [Fact]
    public void Has_GivenOnlyDefaultPalette_RejectsDark()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Default] = CreatePalette();

        Assert.False(FluentThemePalette.Has(resources, AppTheme.Dark));
    }

    [Fact]
    public void Has_GivenLightPalette_AcceptsLight()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Light] = CreatePalette();

        Assert.True(FluentThemePalette.Has(resources, AppTheme.Light));
    }

    [Fact]
    public void Has_GivenOnlyDefaultPalette_AcceptsLight()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Default] = CreatePalette();

        Assert.True(FluentThemePalette.Has(resources, AppTheme.Light));
    }

    [Fact]
    public void Has_GivenPaletteInMergedDictionary_AcceptsIt()
    {
        ResourceDictionary merged = CreateResources();
        merged.ThemeDictionaries[ThemeVariant.Dark] = CreatePalette();
        ResourceDictionary resources = CreateResources();
        resources.MergedDictionaries.Add(merged);

        Assert.True(FluentThemePalette.Has(resources, AppTheme.Dark));
    }

    [Fact]
    public void Has_GivenIncompletePalette_RejectsIt()
    {
        var incomplete = new ResourceDictionary
        {
            [ThemeResourceKeys.SolidBackgroundBrush] = new object(),
        };
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Dark] = incomplete;

        Assert.False(FluentThemePalette.Has(resources, AppTheme.Dark));
    }

    [Fact]
    public void Has_GivenAnyStore_AcceptsSystem()
    {
        Assert.True(FluentThemePalette.Has(CreateResources(), AppTheme.System));
    }

    private static ResourceDictionary CreateResources() => new();

    private static ResourceDictionary CreatePalette() => new()
    {
        [ThemeResourceKeys.SolidBackgroundBrush] = new object(),
        [ThemeResourceKeys.PrimaryTextBrush] = new object(),
    };
}
