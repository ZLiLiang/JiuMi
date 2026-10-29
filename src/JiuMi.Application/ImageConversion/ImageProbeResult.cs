namespace JiuMi.Application.ImageConversion;

/// <summary>
/// What one look at a source file tells the list: its format, size, thumbnail and whether it decodes.
/// </summary>
public sealed class ImageProbeResult : IDisposable
{
    private ImageProbeResult(
        string sourcePath,
        ImageFormat? format,
        int pixelWidth,
        int pixelHeight,
        byte[]? thumbnailPng,
        IDecodedImageHandle? decoded,
        ImageConversionFailureReason? failure,
        Exception? error)
    {
        SourcePath = sourcePath;
        Format = format;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        ThumbnailPng = thumbnailPng;
        Decoded = decoded;
        Failure = failure;
        Error = error;
    }

    /// <summary>
    /// Gets the absolute source file path the probe read.
    /// </summary>
    public string SourcePath
    {
        get;
    }

    /// <summary>
    /// Gets the source format, when its extension is supported.
    /// </summary>
    public ImageFormat? Format
    {
        get;
    }

    /// <summary>
    /// Gets the width of the embedded composite, in pixels.
    /// </summary>
    public int PixelWidth
    {
        get;
    }

    /// <summary>
    /// Gets the height of the embedded composite, in pixels.
    /// </summary>
    public int PixelHeight
    {
        get;
    }

    /// <summary>
    /// Gets the thumbnail as encoded PNG bytes, or <see langword="null"/> when probing failed.
    /// </summary>
    public byte[]? ThumbnailPng
    {
        get;
    }

    /// <summary>
    /// Gets the decoded image the converter reuses, or <see langword="null"/> when probing failed.
    /// </summary>
    public IDecodedImageHandle? Decoded
    {
        get;
    }

    /// <summary>
    /// Gets why the file cannot be converted, or <see langword="null"/> when it can.
    /// </summary>
    public ImageConversionFailureReason? Failure
    {
        get;
    }

    /// <summary>
    /// Gets the failure that caused the probe to fail, for logging, or <see langword="null"/>.
    /// </summary>
    public Exception? Error
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the file decoded and may be converted.
    /// </summary>
    public bool IsDecodable => Failure is null;

    /// <summary>
    /// Creates a successful probe result.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="format">The source format.</param>
    /// <param name="pixelWidth">The composite width.</param>
    /// <param name="pixelHeight">The composite height.</param>
    /// <param name="thumbnailPng">The thumbnail bytes.</param>
    /// <param name="decoded">The decoded image the converter may reuse.</param>
    /// <returns>The result.</returns>
    public static ImageProbeResult Success(
        string sourcePath,
        ImageFormat format,
        int pixelWidth,
        int pixelHeight,
        byte[] thumbnailPng,
        IDecodedImageHandle? decoded = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(thumbnailPng);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        return new ImageProbeResult(
            sourcePath,
            format,
            pixelWidth,
            pixelHeight,
            thumbnailPng,
            decoded,
            failure: null,
            error: null);
    }

    /// <summary>
    /// Creates a probe result for a file that cannot be converted.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="format">The source format when it is known.</param>
    /// <param name="reason">Why the file cannot be converted.</param>
    /// <param name="error">The failure behind the reason, for logging.</param>
    /// <returns>The result.</returns>
    public static ImageProbeResult Failed(
        string sourcePath,
        ImageFormat? format,
        ImageConversionFailureReason reason,
        Exception? error = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return new ImageProbeResult(sourcePath, format, 0, 0, thumbnailPng: null, decoded: null, reason, error);
    }

    /// <inheritdoc />
    public void Dispose() => Decoded?.Dispose();
}
