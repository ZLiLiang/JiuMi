using JiuMi.Application.Logging;
using Serilog.Events;

namespace JiuMi.Application.Tests;

public sealed class LogLevelOverrideTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_GivenNoValue_UsesInformationWithoutARejection(string? value)
    {
        LogLevelOverride result = LogLevelOverride.Parse(value);

        Assert.Equal(LogEventLevel.Information, result.Level);
        Assert.Null(result.RejectedValue);
    }

    [Theory]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("information", LogEventLevel.Information)]
    [InlineData("WARNING", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("fatal", LogEventLevel.Fatal)]
    public void Parse_GivenOneOfTheFiveLevels_UsesItRegardlessOfCase(
        string value,
        LogEventLevel expected)
    {
        LogLevelOverride result = LogLevelOverride.Parse(value);

        Assert.Equal(expected, result.Level);
        Assert.Null(result.RejectedValue);
    }

    [Fact]
    public void Parse_GivenVerbose_RejectsItBecauseVerboseIsNotProduced()
    {
        LogLevelOverride result = LogLevelOverride.Parse("Verbose");

        Assert.Equal(LogEventLevel.Information, result.Level);
        Assert.Equal("Verbose", result.RejectedValue);
    }

    [Fact]
    public void Parse_GivenAnUnknownValue_FallsBackAndKeepsTheRawValue()
    {
        LogLevelOverride result = LogLevelOverride.Parse("Trace");

        Assert.Equal(LogEventLevel.Information, result.Level);
        Assert.Equal("Trace", result.RejectedValue);
    }
}
