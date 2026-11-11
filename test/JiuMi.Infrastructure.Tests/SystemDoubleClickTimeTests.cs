using JiuMi.Infrastructure;

namespace JiuMi.Infrastructure.Tests;

public sealed class SystemDoubleClickTimeTests
{
    [Fact]
    public void Constructor_GivenNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemDoubleClickTime(null!));
    }

    [Fact]
    public void DoubleClickInterval_GivenSystemValue_ReturnsIt()
    {
        var doubleClickTime = new SystemDoubleClickTime(() => 300u);

        Assert.Equal(TimeSpan.FromMilliseconds(300), doubleClickTime.DoubleClickInterval);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public void DoubleClickInterval_GivenNoUsableValue_FallsBackToTheWindowsDefault(uint? value)
    {
        var doubleClickTime = new SystemDoubleClickTime(() => value);

        Assert.Equal(TimeSpan.FromMilliseconds(500), doubleClickTime.DoubleClickInterval);
    }
}
