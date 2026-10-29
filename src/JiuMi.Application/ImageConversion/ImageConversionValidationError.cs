namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The reasons a conversion request is rejected before any file is touched.
/// </summary>
public enum ImageConversionValidationError
{
    /// <summary>The target format is not PNG, JPEG or WebP.</summary>
    TargetFormatNotSupported,

    /// <summary>The quality is outside 1–100.</summary>
    QualityOutOfRange,

    /// <summary>The requested longest edge is zero or negative.</summary>
    MaxEdgeNotPositive,

    /// <summary>The PNG compression level is outside 0–9.</summary>
    PngCompressionLevelOutOfRange,
}
