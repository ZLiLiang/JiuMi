using JiuMi.Application.Settings;
using JiuMi.Theming;

namespace JiuMi.Tests;

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
}
