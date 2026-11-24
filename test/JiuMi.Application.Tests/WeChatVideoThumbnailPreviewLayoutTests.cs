using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoThumbnailPreviewLayoutTests
{
    [Fact]
    public void TryCalculate_PortraitHighResImage_FitsMaxBoundsToRightOfAnchor()
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: 1080,
            imagePixelHeight: 1920,
            anchorLeft: 60,
            anchorTop: 300,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: 1200,
            viewportHeight: 800);

        Assert.NotNull(placement);
        Assert.Equal(236.25, placement.ImageWidth, precision: 2);
        Assert.Equal(420, placement.ImageHeight, precision: 2);
        Assert.Equal(246.25, placement.CardWidth, precision: 2);
        Assert.Equal(430, placement.CardHeight, precision: 2);
        Assert.Equal(126, placement.Left, precision: 2);
        Assert.Equal(113, placement.Top, precision: 2);
    }

    [Fact]
    public void TryCalculate_LandscapeHighResImage_FitsMaxWidthPreservingAspectRatio()
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: 1920,
            imagePixelHeight: 1080,
            anchorLeft: 60,
            anchorTop: 300,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: 1200,
            viewportHeight: 800);

        Assert.NotNull(placement);
        Assert.Equal(320, placement.ImageWidth, precision: 2);
        Assert.Equal(180, placement.ImageHeight, precision: 2);
        Assert.Equal(330, placement.CardWidth, precision: 2);
        Assert.Equal(190, placement.CardHeight, precision: 2);
        Assert.Equal(126, placement.Left, precision: 2);
        Assert.Equal(233, placement.Top, precision: 2);
    }

    [Fact]
    public void TryCalculate_SmallImage_DoesNotUpscaleBeyondOriginalPixels()
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: 120,
            imagePixelHeight: 90,
            anchorLeft: 60,
            anchorTop: 300,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: 1200,
            viewportHeight: 800);

        Assert.NotNull(placement);
        Assert.Equal(120, placement.ImageWidth, precision: 2);
        Assert.Equal(90, placement.ImageHeight, precision: 2);
        Assert.Equal(130, placement.CardWidth, precision: 2);
        Assert.Equal(100, placement.CardHeight, precision: 2);
    }

    [Fact]
    public void TryCalculate_AnchorNearTopEdge_ShiftsDownToAvoidTopClipping()
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: 1080,
            imagePixelHeight: 1920,
            anchorLeft: 60,
            anchorTop: 40,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: 1200,
            viewportHeight: 800);

        Assert.NotNull(placement);
        Assert.Equal(12, placement.Top, precision: 2);
        Assert.True(placement.Top + placement.CardHeight <= 800 - 12);
    }

    [Fact]
    public void TryCalculate_AnchorNearBottomEdge_ShiftsUpToAvoidBottomClipping()
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: 1080,
            imagePixelHeight: 1920,
            anchorLeft: 60,
            anchorTop: 700,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: 1200,
            viewportHeight: 800);

        Assert.NotNull(placement);
        Assert.Equal(800 - 12 - placement.CardHeight, placement.Top, precision: 2);
        Assert.True(placement.Top >= 12);
    }

    [Fact]
    public void TryCalculate_TightViewport_ShrinksPreviewWithinAvailableArea()
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: 1080,
            imagePixelHeight: 1920,
            anchorLeft: 60,
            anchorTop: 100,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: 320,
            viewportHeight: 300);

        Assert.NotNull(placement);
        Assert.True(placement.Left + placement.CardWidth <= 320 - 12 + 0.01);
        Assert.True(placement.Top >= 12);
        Assert.True(placement.Top + placement.CardHeight <= 300 - 12 + 0.01);
        Assert.Equal(1080d / 1920d, placement.ImageWidth / placement.ImageHeight, precision: 4);
    }

    [Theory]
    [InlineData(0, 100, 1200, 800)]
    [InlineData(100, -1, 1200, 800)]
    [InlineData(100, 100, 0, 800)]
    [InlineData(100, 100, 1200, 0)]
    [InlineData(100, 100, 130, 800)]
    [InlineData(double.NaN, 100, 1200, 800)]
    public void TryCalculate_InvalidDimensionsOrNoRoom_ReturnsNull(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight)
    {
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            imagePixelWidth: imageWidth,
            imagePixelHeight: imageHeight,
            anchorLeft: 60,
            anchorTop: 100,
            anchorWidth: 56,
            anchorHeight: 56,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight);

        Assert.Null(placement);
    }
}
