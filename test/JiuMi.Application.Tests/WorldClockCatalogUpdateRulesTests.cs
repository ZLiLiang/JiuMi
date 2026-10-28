using JiuMi.Application.WorldClock;

namespace JiuMi.Application.Tests;

/// <summary>
/// The rules that decide whether a catalog check is due and whether a downloaded file is intact.
/// </summary>
public sealed class WorldClockCatalogUpdateRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDue_GivenNeverChecked_ReturnsTrue()
    {
        Assert.True(WorldClockCatalogUpdatePolicy.IsDue(null, _now));
    }

    [Fact]
    public void IsDue_GivenCheckedWithinADay_ReturnsFalse()
    {
        Assert.False(WorldClockCatalogUpdatePolicy.IsDue(_now - TimeSpan.FromHours(23), _now));
    }

    [Fact]
    public void IsDue_GivenCheckedADayAgo_ReturnsTrue()
    {
        Assert.True(WorldClockCatalogUpdatePolicy.IsDue(_now - TimeSpan.FromHours(24), _now));
    }

    [Fact]
    public void IsDue_GivenClockWentBackwards_ReturnsFalse()
    {
        Assert.False(WorldClockCatalogUpdatePolicy.IsDue(_now + TimeSpan.FromHours(2), _now));
    }

    [Theory]
    [InlineData(20261028, 20261027, true)]
    [InlineData(20261028, 20261028, false)]
    [InlineData(20261027, 20261028, false)]
    [InlineData(1, 0, true)]
    public void IsNewer_GivenTwoVersions_ComparesThem(int candidate, int installed, bool expected)
    {
        Assert.Equal(expected, WorldClockCatalogVersion.IsNewer(candidate, installed));
    }

    [Fact]
    public void Matches_GivenMatchingHash_ReturnsTrue()
    {
        byte[] content = "abc"u8.ToArray();

        Assert.True(WorldClockCatalogIntegrity.Matches(
            content,
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }

    [Fact]
    public void Matches_GivenUppercaseHash_ReturnsTrue()
    {
        byte[] content = "abc"u8.ToArray();

        Assert.True(WorldClockCatalogIntegrity.Matches(
            content,
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"));
    }

    [Fact]
    public void Matches_GivenDifferentContent_ReturnsFalse()
    {
        Assert.False(WorldClockCatalogIntegrity.Matches(
            "abd"u8.ToArray(),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }
}
