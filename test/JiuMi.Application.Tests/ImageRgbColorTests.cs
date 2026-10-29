using JiuMi.Application.ImageConversion;

namespace JiuMi.Application.Tests;

public sealed class ImageRgbColorTests
{
    [Theory]
    [InlineData("#0A1B2C", 0x0A, 0x1B, 0x2C)]
    [InlineData("0a1b2c", 0x0A, 0x1B, 0x2C)]
    [InlineData("  #FFFFFF  ", 0xFF, 0xFF, 0xFF)]
    public void TryParse_GivenSixHexDigits_ParsesChannels(string text, byte r, byte g, byte b)
    {
        Assert.True(ImageRgbColor.TryParse(text, out ImageRgbColor color));
        Assert.Equal(new ImageRgbColor(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("white")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("#FF00FF00")]
    public void TryParse_GivenInvalidText_ReturnsFalse(string? text)
    {
        Assert.False(ImageRgbColor.TryParse(text, out _));
    }

    [Fact]
    public void ToHex_FormatsUpperCaseWithHash()
    {
        Assert.Equal("#0A1B2C", new ImageRgbColor(0x0A, 0x1B, 0x2C).ToHex());
        Assert.Equal("#FFFFFF", ImageRgbColor.White.ToHex());
    }
}
