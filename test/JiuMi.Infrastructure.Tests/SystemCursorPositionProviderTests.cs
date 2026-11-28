using JiuMi.Application.MyJiuMi;
using JiuMi.Infrastructure.MyJiuMi;

namespace JiuMi.Infrastructure.Tests;

public sealed class SystemCursorPositionProviderTests
{
    [Fact]
    public void Constructor_GivenNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemCursorPositionProvider(null!));
    }

    [Fact]
    public void TryGetPosition_GivenAvailablePoint_ReturnsTrueWithCoordinates()
    {
        var expected = new MyJiuMiScreenPoint(640, 360);
        var provider = new SystemCursorPositionProvider(() => expected);

        bool succeeded = provider.TryGetPosition(out MyJiuMiScreenPoint actual);

        Assert.True(succeeded);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGetPosition_GivenUnavailablePoint_ReturnsFalse()
    {
        var provider = new SystemCursorPositionProvider(() => null);

        bool succeeded = provider.TryGetPosition(out MyJiuMiScreenPoint actual);

        Assert.False(succeeded);
        Assert.Equal(default, actual);
    }
}
