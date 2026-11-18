using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoTextTests
{
    [Theory]
    [InlineData(48, "0:48")]
    [InlineData(222, "3:42")]
    [InlineData(495, "8:15")]
    [InlineData(3723, "1:02:03")]
    public void FormatDuration_GivenSeconds_FormatsMinutesAndHours(int seconds, string expected)
    {
        Assert.Equal(expected, WeChatVideoText.FormatDuration(seconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void FormatDuration_GivenMissingOrNonPositive_ReturnsEmpty(int? seconds)
    {
        Assert.Equal(string.Empty, WeChatVideoText.FormatDuration(seconds));
    }

    [Theory]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5120, "5 KB")]
    [InlineData(13_002_342, "12.4 MB")]
    [InlineData(1_610_612_736, "1.5 GB")]
    public void FormatSize_GivenBytes_FormatsARescaledValue(long bytes, string expected)
    {
        Assert.Equal(expected, WeChatVideoText.FormatSize(bytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void FormatSize_GivenMissingOrNonPositive_ReturnsEmpty(long? bytes)
    {
        Assert.Equal(string.Empty, WeChatVideoText.FormatSize(bytes));
    }

    [Fact]
    public void BuildMeta_GivenAllParts_JoinsThemWithTheSeparator()
    {
        Assert.Equal(
            "骑行日记 · 3:42 · 12.4 MB",
            WeChatVideoText.BuildMeta("骑行日记", 222, 13_002_342));
    }

    [Fact]
    public void BuildMeta_GivenMissingParts_SkipsThem()
    {
        Assert.Equal("作者 · 12.4 MB", WeChatVideoText.BuildMeta("作者", null, 13_002_342));
        Assert.Equal("0:48", WeChatVideoText.BuildMeta(null, 48, null));
    }

    [Fact]
    public void BuildMeta_GivenNoParts_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, WeChatVideoText.BuildMeta(null, null, null));
        Assert.Equal(string.Empty, WeChatVideoText.BuildMeta("  ", null, null));
    }
}
