using JiuMi.Application.ImageConversion;

namespace JiuMi.Application.Tests;

public sealed class ImageConversionFailureTests
{
    [Fact]
    public void Priority_FollowsTheDocumentedStageOrder()
    {
        ImageConversionFailureReason[] ordered =
        [
            ImageConversionFailureReason.UnsupportedFormat,
            ImageConversionFailureReason.DecodeFailed,
            ImageConversionFailureReason.EncodeFailed,
            ImageConversionFailureReason.WriteFailed,
        ];

        for (int index = 1; index < ordered.Length; index++)
        {
            Assert.True(
                ImageConversionFailures.Priority(ordered[index - 1]) <
                ImageConversionFailures.Priority(ordered[index]));
        }
    }

    [Fact]
    public void SelectPrimary_GivenEveryStage_ReturnsUnsupportedFormat()
    {
        ImageConversionFailureReason primary = ImageConversionFailures.SelectPrimary(
        [
            ImageConversionFailureReason.WriteFailed,
            ImageConversionFailureReason.EncodeFailed,
            ImageConversionFailureReason.DecodeFailed,
            ImageConversionFailureReason.UnsupportedFormat,
        ]);

        Assert.Equal(ImageConversionFailureReason.UnsupportedFormat, primary);
    }

    [Fact]
    public void SelectPrimary_GivenWriteAndEncode_ReturnsEncode()
    {
        ImageConversionFailureReason primary = ImageConversionFailures.SelectPrimary(
        [
            ImageConversionFailureReason.WriteFailed,
            ImageConversionFailureReason.EncodeFailed,
        ]);

        Assert.Equal(ImageConversionFailureReason.EncodeFailed, primary);
    }

    [Fact]
    public void SelectPrimary_GivenNoReasons_Throws()
    {
        Assert.Throws<ArgumentException>(() => ImageConversionFailures.SelectPrimary([]));
    }
}
