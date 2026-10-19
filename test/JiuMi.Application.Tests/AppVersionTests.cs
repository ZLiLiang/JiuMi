namespace JiuMi.Application.Tests;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3+abcdef", "1.2.3")]
    [InlineData("1.2.3-beta.1+abcdef", "1.2.3-beta.1")]
    [InlineData("  1.2.3  ", "1.2.3")]
    [InlineData("+abcdef", "0.0.0")]
    [InlineData("", "0.0.0")]
    [InlineData("   ", "0.0.0")]
    [InlineData(null, "0.0.0")]
    public void FromInformationalVersion_GivenVersionString_ReturnsSemanticVersion(
        string? informationalVersion,
        string expected)
    {
        string actual = AppVersion.FromInformationalVersion(informationalVersion);

        Assert.Equal(expected, actual);
    }
}
