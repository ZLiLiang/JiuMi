using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenameExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_GivenNoText_ReturnsNoFilter(string? text)
    {
        Assert.Empty(RenameExtensions.Parse(text));
    }

    [Fact]
    public void Parse_GivenSpacesAndDots_ReturnsNormalizedDistinctExtensions()
    {
        Assert.Equal(
            [".png", ".jpg"],
            RenameExtensions.Parse(" .PNG  png;*.jpg,.JPG "));
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.PNG", true)]
    [InlineData("a.jpg", false)]
    public void Allows_GivenFilter_MatchesCaseInsensitively(string name, bool expected)
    {
        IReadOnlyList<string> extensions = RenameExtensions.Parse(".png");

        Assert.Equal(expected, RenameExtensions.Allows(name, extensions));
    }

    [Fact]
    public void Allows_GivenEmptyFilter_AllowsEveryName()
    {
        Assert.True(RenameExtensions.Allows("a.txt", []));
    }
}
