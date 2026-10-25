using Avalonia.Styling;
using JiuMi.Application.Settings;
using JiuMi.Theming;

namespace JiuMi.Tests;

[Collection(AvaloniaTestGroup.Name)]
public sealed class AvaloniaThemeApplierTests
{
    [Fact]
    public void TryApply_GivenNoApplication_ReturnsFalseWithoutFailure()
    {
        var applier = new AvaloniaThemeApplier(static () => null);

        bool applied = applier.TryApply(AppTheme.Dark, out Exception? failure);

        Assert.False(applied);
        Assert.Null(failure);
    }

    [Fact]
    public void TryApply_GivenTheFluentTheme_SwitchesEveryPreference()
    {
        HeadlessTest.Run(() =>
        {
            var applier = new AvaloniaThemeApplier(
                static () => global::Avalonia.Application.Current);

            Assert.True(applier.TryApply(AppTheme.Dark, out Exception? failure));
            Assert.Null(failure);
            Assert.Equal(
                ThemeVariant.Dark,
                global::Avalonia.Application.Current!.RequestedThemeVariant);

            Assert.True(applier.TryApply(AppTheme.Light, out failure));
            Assert.Null(failure);
            Assert.Equal(
                ThemeVariant.Light,
                global::Avalonia.Application.Current.RequestedThemeVariant);

            Assert.True(applier.TryApply(AppTheme.System, out failure));
            Assert.Null(failure);
            Assert.Equal(
                ThemeVariant.Default,
                global::Avalonia.Application.Current.RequestedThemeVariant);
        });
    }
}
