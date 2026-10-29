namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The collapsed advanced parameters of a conversion request. They are never persisted or remembered.
/// </summary>
public sealed record ImageConversionAdvancedOptions
{
    /// <summary>
    /// Gets a value indicating whether WebP output is lossless.
    /// </summary>
    public bool WebPLossless
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the PNG compression level, from 0 (fastest) to 9 (smallest).
    /// </summary>
    public int PngCompressionLevel
    {
        get;
        init;
    } = ImageConversionRules.DefaultPngCompressionLevel;

    /// <summary>
    /// Gets a value indicating whether the source EXIF/GPS data is written to the output.
    /// </summary>
    public bool KeepSourceMetadata
    {
        get;
        init;
    }

    /// <summary>
    /// Creates the advanced options used before the user changes anything.
    /// </summary>
    /// <returns>The default advanced options.</returns>
    public static ImageConversionAdvancedOptions CreateDefault() => new();
}
