namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The image conversion parameters kept between sessions: where outputs go and the JPEG background.
/// </summary>
public sealed record ImageConvertSettings
{
    /// <summary>
    /// Gets the remembered output directory, or <see langword="null"/> to write beside each source.
    /// </summary>
    public string? OutputDirectory
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the colour that replaces transparency in JPEG output.
    /// </summary>
    public ImageRgbColor JpegBackgroundColor
    {
        get;
        init;
    } = ImageRgbColor.White;

    /// <summary>
    /// Creates the settings used before the user changes anything.
    /// </summary>
    /// <returns>The defaults: source-directory output and a white background.</returns>
    public static ImageConvertSettings CreateDefault() => new();
}
