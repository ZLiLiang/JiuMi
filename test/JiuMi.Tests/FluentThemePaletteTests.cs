using Avalonia.Controls;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using JiuMi.Application.Settings;
using JiuMi.Theming;

namespace JiuMi.Tests;

[Collection(AvaloniaTestGroup.Name)]
public sealed class FluentThemePaletteTests
{
    [Fact]
    public void HasPaletteFor_GivenDarkPalette_AcceptsDark()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Dark] = CreatePalette();

        Assert.True(FluentThemePalette.HasPaletteFor(resources, AppTheme.Dark));
    }

    [Fact]
    public void HasPaletteFor_GivenOnlyDefaultPalette_RejectsDark()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Default] = CreatePalette();

        Assert.False(FluentThemePalette.HasPaletteFor(resources, AppTheme.Dark));
    }

    [Fact]
    public void HasPaletteFor_GivenLightPalette_AcceptsLight()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Light] = CreatePalette();

        Assert.True(FluentThemePalette.HasPaletteFor(resources, AppTheme.Light));
    }

    [Fact]
    public void HasPaletteFor_GivenOnlyDefaultPalette_AcceptsLight()
    {
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Default] = CreatePalette();

        Assert.True(FluentThemePalette.HasPaletteFor(resources, AppTheme.Light));
    }

    [Fact]
    public void HasPaletteFor_GivenPaletteInMergedDictionary_AcceptsIt()
    {
        ResourceDictionary merged = CreateResources();
        merged.ThemeDictionaries[ThemeVariant.Dark] = CreatePalette();
        ResourceDictionary resources = CreateResources();
        resources.MergedDictionaries.Add(merged);

        Assert.True(FluentThemePalette.HasPaletteFor(resources, AppTheme.Dark));
    }

    [Fact]
    public void HasPaletteFor_GivenIncompletePalette_RejectsIt()
    {
        var incomplete = new ResourceDictionary
        {
            [ThemeResourceKeys.SolidBackgroundBrush] = new object(),
        };
        ResourceDictionary resources = CreateResources();
        resources.ThemeDictionaries[ThemeVariant.Dark] = incomplete;

        Assert.False(FluentThemePalette.HasPaletteFor(resources, AppTheme.Dark));
    }

    [Fact]
    public void HasPaletteFor_GivenAnyStore_AcceptsSystem()
    {
        Assert.True(FluentThemePalette.HasPaletteFor(CreateResources(), AppTheme.System));
    }

    [Fact]
    public void HasPaletteFor_GivenTheFluentTheme_AcceptsBothExplicitThemes()
    {
        HeadlessTest.Run(() =>
        {
            // The synthetic dictionaries above cannot detect a probe that rejects the real Fluent layout.
            var application = Assert.IsType<global::Avalonia.Application>(
                global::Avalonia.Application.Current);
            var theme = Assert.Single(application.Styles.OfType<FluentAvaloniaTheme>());

            Assert.True(FluentThemePalette.HasPaletteFor(theme.Resources, AppTheme.Light));
            Assert.True(FluentThemePalette.HasPaletteFor(theme.Resources, AppTheme.Dark));
        });
    }

    private static ResourceDictionary CreateResources() => new();

    private static ResourceDictionary CreatePalette() => new()
    {
        [ThemeResourceKeys.SolidBackgroundBrush] = new object(),
        [ThemeResourceKeys.PrimaryTextBrush] = new object(),
    };
}
