namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Computed position and dimensions for the thumbnail hover preview card within the host viewport.
/// </summary>
/// <param name="Left">Horizontal offset of the card from the viewport's left edge, in DIPs.</param>
/// <param name="Top">Vertical offset of the card from the viewport's top edge, in DIPs.</param>
/// <param name="ImageWidth">Rendered width of the cover image, in DIPs.</param>
/// <param name="ImageHeight">Rendered height of the cover image, in DIPs.</param>
/// <param name="CardWidth">Total width of the preview card including frame padding and border, in DIPs.</param>
/// <param name="CardHeight">Total height of the preview card including frame padding and border, in DIPs.</param>
public sealed record WeChatVideoThumbnailPreviewPlacement(
    double Left,
    double Top,
    double ImageWidth,
    double ImageHeight,
    double CardWidth,
    double CardHeight);
