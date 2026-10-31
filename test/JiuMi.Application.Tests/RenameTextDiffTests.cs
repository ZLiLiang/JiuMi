using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenameTextDiffTests
{
    [Fact]
    public void Compute_GivenChangedMiddle_ReturnsPrefixChangedAndSuffix()
    {
        RenameTextDiff diff = RenameTextDiff.Compute("产品图-01.png", "product-01.png");

        Assert.Equal(string.Empty, diff.Prefix);
        Assert.Equal("product", diff.Changed);
        Assert.Equal("-01.png", diff.Suffix);
    }

    [Fact]
    public void Compute_GivenCaseOnlyChange_ReturnsTheChangedSuffix()
    {
        RenameTextDiff diff = RenameTextDiff.Compute("photo.png", "photo.PNG");

        Assert.Equal("photo.", diff.Prefix);
        Assert.Equal("PNG", diff.Changed);
        Assert.Equal(string.Empty, diff.Suffix);
    }

    [Fact]
    public void Compute_GivenIdenticalNames_ReturnsNoChangedPart()
    {
        RenameTextDiff diff = RenameTextDiff.Compute("a.txt", "a.txt");

        Assert.Equal("a.txt", diff.Prefix);
        Assert.Equal(string.Empty, diff.Changed);
        Assert.Equal(string.Empty, diff.Suffix);
    }

    [Fact]
    public void Compute_GivenAddedPrefix_KeepsTheSuffix()
    {
        RenameTextDiff diff = RenameTextDiff.Compute("a.txt", "2026-a.txt");

        Assert.Equal(string.Empty, diff.Prefix);
        Assert.Equal("2026-", diff.Changed);
        Assert.Equal("a.txt", diff.Suffix);
    }
}
