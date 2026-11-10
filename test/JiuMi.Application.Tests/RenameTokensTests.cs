using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenameTokensTests
{
    private static readonly DateOnly _today = new(2026, 10, 31);

    [Fact]
    public void ExpandTemplate_GivenAllTokens_SubstitutesEachOne()
    {
        string result = RenameTokens.ExpandTemplate(
            "{date}-{index:00}-{name}{ext}",
            "产品图",
            ".png",
            3,
            120,
            _today);

        Assert.Equal("2026-10-31-003-产品图.png", result);
    }

    [Fact]
    public void ExpandTemplate_GivenIndexWithoutWidth_DoesNotPad()
    {
        string result = RenameTokens.ExpandTemplate("{index}", "a", "", 7, 120, _today);

        Assert.Equal("7", result);
    }

    [Fact]
    public void ExpandTemplate_GivenExplicitWidthSmallerThanCount_UsesCountDigits()
    {
        string result = RenameTokens.ExpandTemplate("{index:00}", "a", "", 5, 120, _today);

        Assert.Equal("005", result);
    }

    [Fact]
    public void ExpandTemplate_GivenExplicitWidthLargerThanCount_KeepsTheWidth()
    {
        string result = RenameTokens.ExpandTemplate("{index:000}", "a", "", 5, 12, _today);

        Assert.Equal("005", result);
    }

    [Fact]
    public void ExpandTemplate_GivenUnknownToken_LeavesItLiteral()
    {
        string result = RenameTokens.ExpandTemplate("{other}-{name}", "a", ".txt", 1, 1, _today);

        Assert.Equal("{other}-a", result);
    }

    [Fact]
    public void ExpandTemplate_GivenBracesWithoutClosing_LeavesThemLiteral()
    {
        string result = RenameTokens.ExpandTemplate("{name", "a", "", 1, 1, _today);

        Assert.Equal("{name", result);
    }

    [Fact]
    public void ExpandIndex_GivenIndexTokens_ExpandsOnlyThose()
    {
        string result = RenameTokens.ExpandIndex("{index}-{name}-{index:00}", 2, 120);

        Assert.Equal("2-{name}-002", result);
    }

    [Theory]
    [InlineData(0, "{index}")]
    [InlineData(2, "{index:00}")]
    [InlineData(4, "{index:0000}")]
    public void IndexToken_GivenWidth_ReturnsToken(int width, string expected)
    {
        Assert.Equal(expected, RenameTokens.IndexToken(width));
    }
}
