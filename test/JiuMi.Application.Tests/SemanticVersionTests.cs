using JiuMi.Application.Updates;

namespace JiuMi.Application.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.4.0", 1, 4, 0, false, "1.4.0")]
    [InlineData("1.5.0-preview.1", 1, 5, 0, true, "1.5.0-preview.1")]
    [InlineData("0.0.1-alpha.1.beta+20261122", 0, 0, 1, true, "0.0.1-alpha.1.beta")]
    [InlineData("1.4.0+abcdef", 1, 4, 0, false, "1.4.0")]
    public void TryParse_GivenValidVersion_ParsesComponents(
        string text,
        int major,
        int minor,
        int patch,
        bool isPrerelease,
        string canonical)
    {
        bool parsed = SemanticVersion.TryParse(text, out SemanticVersion? version);

        Assert.True(parsed);
        Assert.NotNull(version);
        Assert.Equal(major, version!.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(isPrerelease, version.IsPrerelease);
        Assert.Equal(canonical, version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v1.4.0")]
    [InlineData("1.4")]
    [InlineData("1.4.0.0")]
    [InlineData("01.4.0")]
    [InlineData("1.04.0")]
    [InlineData("1.4.00")]
    [InlineData("-1.0.0")]
    [InlineData("1.4.0-")]
    [InlineData("1.4.0-preview..1")]
    [InlineData("1.4.0-preview.01")]
    [InlineData("1.4.0-preview_1")]
    [InlineData("1.4.0+")]
    [InlineData("1.4.0+abc..def")]
    public void TryParse_GivenInvalidVersion_ReturnsFalse(string? text)
    {
        bool parsed = SemanticVersion.TryParse(text, out SemanticVersion? version);

        Assert.False(parsed);
        Assert.Null(version);
    }

    [Theory]
    [InlineData("1.4.0-preview.9", "1.4.0-preview.10")]
    [InlineData("1.4.0-preview.2", "1.4.0")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-1", "1.0.0-alpha")]
    [InlineData("1.0.0-beta", "1.0.0-beta.2")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.4.0", "1.4.1")]
    [InlineData("1.4.1", "1.5.0-preview.1")]
    [InlineData("1.5.0", "2.0.0-preview.1")]
    public void CompareTo_GivenOrderedVersions_OrdersBySemVerPrecedence(string lowerText, string higherText)
    {
        SemanticVersion lower = SemanticVersion.Parse(lowerText);
        SemanticVersion higher = SemanticVersion.Parse(higherText);

        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.True(lower.CompareTo(higher) < 0);
        Assert.True(higher.CompareTo(lower) > 0);
        Assert.NotEqual(lower, higher);
    }

    [Fact]
    public void Equals_GivenSameVersionWithDifferentBuildMetadata_ReturnsTrue()
    {
        SemanticVersion left = SemanticVersion.Parse("1.4.0-preview.1+abc");
        SemanticVersion right = SemanticVersion.Parse("1.4.0-preview.1+def");

        Assert.Equal(left, right);
        Assert.Equal(0, left.CompareTo(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left == right);
        Assert.False(left != right);
    }
}
