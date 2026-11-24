namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Computes the size and placement of the WeChat video thumbnail hover preview card.
/// </summary>
public static class WeChatVideoThumbnailPreviewLayout
{
    private const double MaxImageWidth = 320;
    private const double MaxImageHeight = 420;
    private const double AnchorGap = 10;
    private const double EdgeMargin = 12;

    /// <summary>
    /// Gets the delay before showing the preview while the pointer stays on a thumbnail.
    /// </summary>
    public static TimeSpan HoverDelay { get; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Inner padding around the preview image inside the card frame, in DIPs.
    /// </summary>
    public const double CardPadding = 4;

    /// <summary>
    /// Outer border thickness of the preview card frame, in DIPs.
    /// </summary>
    public const double CardBorderThickness = 1;

    /// <summary>
    /// Calculates the preview card size and position for a cover image anchored to a thumbnail.
    /// </summary>
    /// <param name="imagePixelWidth">Actual pixel width of the decoded cover bitmap.</param>
    /// <param name="imagePixelHeight">Actual pixel height of the decoded cover bitmap.</param>
    /// <param name="anchorLeft">Thumbnail left coordinate in viewport space.</param>
    /// <param name="anchorTop">Thumbnail top coordinate in viewport space.</param>
    /// <param name="anchorWidth">Thumbnail width in viewport space.</param>
    /// <param name="anchorHeight">Thumbnail height in viewport space.</param>
    /// <param name="viewportWidth">Host window viewport width.</param>
    /// <param name="viewportHeight">Host window viewport height.</param>
    /// <returns>The computed placement, or <see langword="null"/> when inputs are invalid or no room is available.</returns>
    public static WeChatVideoThumbnailPreviewPlacement? TryCalculate(
        double imagePixelWidth,
        double imagePixelHeight,
        double anchorLeft,
        double anchorTop,
        double anchorWidth,
        double anchorHeight,
        double viewportWidth,
        double viewportHeight)
    {
        if (!IsPositiveFinite(imagePixelWidth) ||
            !IsPositiveFinite(imagePixelHeight) ||
            !IsPositiveFinite(viewportWidth) ||
            !IsPositiveFinite(viewportHeight) ||
            !double.IsFinite(anchorLeft) ||
            !double.IsFinite(anchorTop) ||
            !double.IsFinite(anchorWidth) ||
            !double.IsFinite(anchorHeight) ||
            anchorWidth < 0 ||
            anchorHeight < 0)
        {
            return null;
        }

        double left = anchorLeft + anchorWidth + AnchorGap;
        double cardChrome = (CardPadding + CardBorderThickness) * 2;
        double availableRightWidth = viewportWidth - EdgeMargin - left;
        double availableHeight = viewportHeight - (EdgeMargin * 2);

        double maxImageWidth = Math.Min(MaxImageWidth, availableRightWidth - cardChrome);
        double maxImageHeight = Math.Min(MaxImageHeight, availableHeight - cardChrome);
        if (maxImageWidth <= 0 || maxImageHeight <= 0)
        {
            return null;
        }

        double scale = Math.Min(
            1.0,
            Math.Min(maxImageWidth / imagePixelWidth, maxImageHeight / imagePixelHeight));

        double imageWidth = imagePixelWidth * scale;
        double imageHeight = imagePixelHeight * scale;
        double cardWidth = imageWidth + cardChrome;
        double cardHeight = imageHeight + cardChrome;

        double idealTop = anchorTop + ((anchorHeight - cardHeight) / 2.0);
        double maxTop = Math.Max(EdgeMargin, viewportHeight - EdgeMargin - cardHeight);
        double top = Math.Clamp(idealTop, EdgeMargin, maxTop);

        return new WeChatVideoThumbnailPreviewPlacement(
            left,
            top,
            imageWidth,
            imageHeight,
            cardWidth,
            cardHeight);
    }

    private static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0;
}
