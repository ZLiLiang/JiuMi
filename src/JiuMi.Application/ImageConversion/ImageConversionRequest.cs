namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The parameters that stay fixed for one batch. They travel to every worker unchanged.
/// </summary>
public sealed record ImageConversionRequest
{
    /// <summary>
    /// Gets the format written for every source file.
    /// </summary>
    public ImageFormat TargetFormat
    {
        get;
        init;
    } = ImageFormat.Png;

    /// <summary>
    /// Gets the quality, from 1 to 100; it only applies to JPEG and lossy WebP output.
    /// </summary>
    public int Quality
    {
        get;
        init;
    } = ImageConversionRules.DefaultQuality;

    /// <summary>
    /// Gets the longest edge to fit inside, or <see langword="null"/> to keep the source size.
    /// The image is never enlarged.
    /// </summary>
    public int? MaxEdge
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the colour that replaces transparency when the target is JPEG.
    /// </summary>
    public ImageRgbColor JpegBackgroundColor
    {
        get;
        init;
    } = ImageRgbColor.White;

    /// <summary>
    /// Gets the output directory, or <see langword="null"/> to write beside each source file.
    /// </summary>
    public string? OutputDirectory
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the advanced parameters from the collapsed section.
    /// </summary>
    public ImageConversionAdvancedOptions Advanced
    {
        get;
        init;
    } = ImageConversionAdvancedOptions.CreateDefault();
}
