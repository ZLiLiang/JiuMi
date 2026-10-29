using JiuMi.Application.ImageConversion;

namespace JiuMi.Application.Tests;

public sealed class ImageConversionRulesTests
{
    [Fact]
    public void Validate_GivenDefaults_ReturnsNoErrors()
    {
        Assert.Empty(ImageConversionRules.Validate(new ImageConversionRequest()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_GivenQualityOutsideRange_ReportsQuality(int quality)
    {
        var request = new ImageConversionRequest { Quality = quality };

        Assert.Equal(
            [ImageConversionValidationError.QualityOutOfRange],
            ImageConversionRules.Validate(request));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validate_GivenQualityAtTheBounds_AcceptsIt(int quality)
    {
        Assert.Empty(ImageConversionRules.Validate(new ImageConversionRequest { Quality = quality }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_GivenNonPositiveMaxEdge_ReportsMaxEdge(int maxEdge)
    {
        var request = new ImageConversionRequest { MaxEdge = maxEdge };

        Assert.Equal(
            [ImageConversionValidationError.MaxEdgeNotPositive],
            ImageConversionRules.Validate(request));
    }

    [Fact]
    public void Validate_GivenNullMaxEdge_AcceptsOriginalSize()
    {
        Assert.Empty(ImageConversionRules.Validate(new ImageConversionRequest { MaxEdge = null }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Validate_GivenPngCompressionOutsideRange_ReportsCompression(int level)
    {
        var request = new ImageConversionRequest
        {
            Advanced = new ImageConversionAdvancedOptions { PngCompressionLevel = level },
        };

        Assert.Equal(
            [ImageConversionValidationError.PngCompressionLevelOutOfRange],
            ImageConversionRules.Validate(request));
    }

    [Fact]
    public void Validate_GivenSourceOnlyTargetFormat_ReportsTarget()
    {
        var request = new ImageConversionRequest { TargetFormat = ImageFormat.Psd };

        Assert.Equal(
            [ImageConversionValidationError.TargetFormatNotSupported],
            ImageConversionRules.Validate(request));
    }

    [Fact]
    public void Validate_GivenEveryViolation_ReportsThemInFixedOrder()
    {
        var request = new ImageConversionRequest
        {
            TargetFormat = ImageFormat.Bmp,
            Quality = 0,
            MaxEdge = 0,
            Advanced = new ImageConversionAdvancedOptions { PngCompressionLevel = 12 },
        };

        Assert.Equal(
            [
                ImageConversionValidationError.TargetFormatNotSupported,
                ImageConversionValidationError.QualityOutOfRange,
                ImageConversionValidationError.MaxEdgeNotPositive,
                ImageConversionValidationError.PngCompressionLevelOutOfRange,
            ],
            ImageConversionRules.Validate(request));
    }

    [Fact]
    public void Validate_GivenWebPLosslessWithTheOtherAdvancedOptions_AcceptsTheCombination()
    {
        var request = new ImageConversionRequest
        {
            TargetFormat = ImageFormat.WebP,
            Quality = 75,
            Advanced = new ImageConversionAdvancedOptions
            {
                WebPLossless = true,
                PngCompressionLevel = 0,
                KeepSourceMetadata = true,
            },
        };

        Assert.Empty(ImageConversionRules.Validate(request));
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.WebP)]
    public void Validate_GivenPngCompressionBoundaryOnAnotherTarget_AcceptsIt(ImageFormat target)
    {
        var request = new ImageConversionRequest
        {
            TargetFormat = target,
            Advanced = new ImageConversionAdvancedOptions
            {
                PngCompressionLevel = ImageConversionRules.MaxPngCompressionLevel,
            },
        };

        Assert.Empty(ImageConversionRules.Validate(request));
    }
}
