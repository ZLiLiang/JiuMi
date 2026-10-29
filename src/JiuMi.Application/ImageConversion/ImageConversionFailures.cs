namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Helpers around <see cref="ImageConversionFailureReason"/>.
/// </summary>
public static class ImageConversionFailures
{
    /// <summary>
    /// Gets the fixed priority of a reason: unsupported format, decode, encode, write.
    /// </summary>
    /// <param name="reason">The reason to rank.</param>
    /// <returns>A smaller number for a reason that wins over the others.</returns>
    public static int Priority(ImageConversionFailureReason reason) => (int)reason;

    /// <summary>
    /// Selects the reason that should be shown when a file failed in more than one stage.
    /// </summary>
    /// <param name="reasons">The reasons collected for the file.</param>
    /// <returns>The highest-priority reason.</returns>
    public static ImageConversionFailureReason SelectPrimary(IEnumerable<ImageConversionFailureReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        ImageConversionFailureReason? primary = null;
        foreach (ImageConversionFailureReason reason in reasons)
        {
            if (primary is null || Priority(reason) < Priority(primary.Value))
            {
                primary = reason;
            }
        }

        return primary ?? throw new ArgumentException("At least one reason is required.", nameof(reasons));
    }
}
