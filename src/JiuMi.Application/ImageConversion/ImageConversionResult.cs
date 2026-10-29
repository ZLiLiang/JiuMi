namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The outcome of converting one file.
/// </summary>
/// <param name="SourcePath">The source file path.</param>
/// <param name="OutputPath">The written file, or <see langword="null"/> on failure.</param>
/// <param name="Failure">Why the file failed, or <see langword="null"/> on success.</param>
/// <param name="Error">The failure behind the reason, for logging.</param>
public sealed record ImageConversionResult(
    string SourcePath,
    string? OutputPath,
    ImageConversionFailureReason? Failure,
    Exception? Error = null)
{
    /// <summary>
    /// Gets a value indicating whether the output was written.
    /// </summary>
    public bool Succeeded => Failure is null;

    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="outputPath">The written file.</param>
    /// <returns>The outcome.</returns>
    public static ImageConversionResult Success(string sourcePath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return new ImageConversionResult(sourcePath, outputPath, Failure: null);
    }

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="reason">Why the file failed.</param>
    /// <param name="error">The failure behind the reason, for logging.</param>
    /// <returns>The outcome.</returns>
    public static ImageConversionResult Failed(
        string sourcePath,
        ImageConversionFailureReason reason,
        Exception? error = null) =>
        new(sourcePath, OutputPath: null, reason, error);
}
