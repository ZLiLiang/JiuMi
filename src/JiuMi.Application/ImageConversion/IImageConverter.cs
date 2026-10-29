namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Writes one source file as the requested target format. This is the only seam between the tool
/// page and the imaging engine.
/// </summary>
public interface IImageConverter
{
    /// <summary>
    /// Gets a value indicating whether the imaging engine loaded. When it is false the tool page
    /// disables the start button and explains the degradation in place.
    /// </summary>
    bool IsAvailable
    {
        get;
    }

    /// <summary>
    /// Converts a probed source file into <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="probe">The successful probe whose decoded image may be reused.</param>
    /// <param name="request">The batch parameters.</param>
    /// <param name="outputPath">The planned output path; an existing file is never overwritten.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome; failures are values, not exceptions.</returns>
    Task<ImageConversionResult> ConvertAsync(
        ImageProbeResult probe,
        ImageConversionRequest request,
        string outputPath,
        CancellationToken cancellationToken = default);
}
