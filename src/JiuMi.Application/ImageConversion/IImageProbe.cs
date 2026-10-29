namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Reads a source file once: its format, size, thumbnail and decodability. The decoded image stays
/// alive until the row is converted or removed, so the conversion does not decode the file again.
/// </summary>
public interface IImageProbe
{
    /// <summary>
    /// Gets a value indicating whether the imaging engine loaded. When it is false the tool page
    /// stays visible but conversion is disabled.
    /// </summary>
    bool IsAvailable
    {
        get;
    }

    /// <summary>
    /// Probes one source file.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="thumbnailMaxEdge">The longest thumbnail edge; the thumbnail is never enlarged.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The probe result, which owns the decoded image until disposed.</returns>
    Task<ImageProbeResult> ProbeAsync(
        string sourcePath,
        int thumbnailMaxEdge,
        CancellationToken cancellationToken = default);
}
