namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Maps file extensions to formats and describes what each format can carry.
/// </summary>
public static class ImageFormatCatalog
{
    private static readonly ImageFormat[] _outputFormats = [ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.WebP];

    /// <summary>
    /// Gets the target formats offered to the user.
    /// </summary>
    public static IReadOnlyList<ImageFormat> OutputFormats => _outputFormats;

    /// <summary>
    /// Resolves a source file's format from its extension; the comparison ignores case.
    /// </summary>
    /// <param name="path">The source file path.</param>
    /// <param name="format">The resolved format when the extension is a supported input.</param>
    /// <returns><see langword="true"/> when the file may be converted.</returns>
    public static bool TryGetSourceFormat(string path, out ImageFormat format)
    {
        format = default;
        return !string.IsNullOrWhiteSpace(path) && TryGetFormat(Path.GetExtension(path), out format);
    }

    /// <summary>
    /// Resolves a format from a file extension, including a leading dot; the comparison ignores case.
    /// </summary>
    /// <param name="extension">The extension to resolve.</param>
    /// <param name="format">The resolved format.</param>
    /// <returns><see langword="true"/> when the extension belongs to a known format.</returns>
    public static bool TryGetFormat(string extension, out ImageFormat format)
    {
        format = extension?.ToLowerInvariant() switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".webp" => ImageFormat.WebP,
            ".bmp" => ImageFormat.Bmp,
            ".psd" => ImageFormat.Psd,
            ".psb" => ImageFormat.Psb,
            _ => (ImageFormat)(-1),
        };

        return (int)format >= 0;
    }

    /// <summary>
    /// Gets a value indicating whether a format can be a conversion target.
    /// </summary>
    /// <param name="format">The format to test.</param>
    /// <returns><see langword="true"/> for PNG, JPEG and WebP.</returns>
    public static bool IsOutputFormat(ImageFormat format) =>
        format is ImageFormat.Png or ImageFormat.Jpeg or ImageFormat.WebP;

    /// <summary>
    /// Gets a value indicating whether a format can store an alpha channel.
    /// </summary>
    /// <param name="format">The format to test.</param>
    /// <returns><see langword="false"/> only for JPEG.</returns>
    public static bool SupportsTransparency(ImageFormat format) => format != ImageFormat.Jpeg;

    /// <summary>
    /// Gets a value indicating whether the quality setting applies to a format.
    /// </summary>
    /// <param name="format">The format to test.</param>
    /// <returns><see langword="true"/> for JPEG and WebP.</returns>
    public static bool IsLossy(ImageFormat format) => format is ImageFormat.Jpeg or ImageFormat.WebP;

    /// <summary>
    /// Gets a value indicating whether the format is a Photoshop document read through its composite.
    /// </summary>
    /// <param name="format">The format to test.</param>
    /// <returns><see langword="true"/> for PSD and PSB.</returns>
    public static bool IsPhotoshopDocument(ImageFormat format) => format is ImageFormat.Psd or ImageFormat.Psb;

    /// <summary>
    /// Gets the file extension written for a target format.
    /// </summary>
    /// <param name="format">The target format.</param>
    /// <returns>The extension including its leading dot.</returns>
    public static string GetExtension(ImageFormat format) => format switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.WebP => ".webp",
        ImageFormat.Bmp => ".bmp",
        ImageFormat.Psd => ".psd",
        ImageFormat.Psb => ".psb",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown image format."),
    };

    /// <summary>
    /// Gets the short name shown in the interface and in row descriptions.
    /// </summary>
    /// <param name="format">The format to name.</param>
    /// <returns>An uppercase short name such as <c>PNG</c>.</returns>
    public static string GetDisplayName(ImageFormat format) => format switch
    {
        ImageFormat.Png => "PNG",
        ImageFormat.Jpeg => "JPEG",
        ImageFormat.WebP => "WebP",
        ImageFormat.Bmp => "BMP",
        ImageFormat.Psd => "PSD",
        ImageFormat.Psb => "PSB",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown image format."),
    };
}
