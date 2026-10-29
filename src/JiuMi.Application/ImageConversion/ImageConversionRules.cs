namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The values a conversion request must stay inside.
/// </summary>
public static class ImageConversionRules
{
    /// <summary>The lowest accepted quality.</summary>
    public const int MinQuality = 1;

    /// <summary>The highest accepted quality.</summary>
    public const int MaxQuality = 100;

    /// <summary>The quality used before the user moves the slider.</summary>
    public const int DefaultQuality = 90;

    /// <summary>The fastest PNG compression level.</summary>
    public const int MinPngCompressionLevel = 0;

    /// <summary>The smallest PNG compression level.</summary>
    public const int MaxPngCompressionLevel = 9;

    /// <summary>The PNG compression level used before the user moves the slider.</summary>
    public const int DefaultPngCompressionLevel = 6;

    /// <summary>
    /// Checks the request against the value ranges and the supported target formats.
    /// </summary>
    /// <param name="request">The request to check.</param>
    /// <returns>The violations, empty when the request may run.</returns>
    public static IReadOnlyList<ImageConversionValidationError> Validate(ImageConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ImageConversionValidationError>(4);
        if (!ImageFormatCatalog.IsOutputFormat(request.TargetFormat))
        {
            errors.Add(ImageConversionValidationError.TargetFormatNotSupported);
        }

        if (request.Quality is < MinQuality or > MaxQuality)
        {
            errors.Add(ImageConversionValidationError.QualityOutOfRange);
        }

        if (request.MaxEdge is <= 0)
        {
            errors.Add(ImageConversionValidationError.MaxEdgeNotPositive);
        }

        if (request.Advanced.PngCompressionLevel is < MinPngCompressionLevel or > MaxPngCompressionLevel)
        {
            errors.Add(ImageConversionValidationError.PngCompressionLevelOutOfRange);
        }

        return errors;
    }
}
