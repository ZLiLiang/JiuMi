using JiuMi.Infrastructure;

namespace JiuMi.Infrastructure.Tests;

public sealed class SystemAnimationSettingsTests
{
    [Fact]
    public void Constructor_GivenNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemAnimationSettings(null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AreClientAreaAnimationsEnabled_GivenReaderValue_ReturnsReaderValue(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var settings = new SystemAnimationSettings(() => enabled);

        Assert.Equal(enabled, settings.AreClientAreaAnimationsEnabled);
    }

    [Fact]
    public void AreClientAreaAnimationsEnabled_GivenFailedRead_ReturnsTrue()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var settings = new SystemAnimationSettings(() => null);

        Assert.True(settings.AreClientAreaAnimationsEnabled);
    }
}
