using ImageMagick;

namespace JiuMi.Infrastructure.ImageConversion;

/// <summary>
/// Loads the native imaging library once and reports whether it is usable.
/// </summary>
internal static class MagickEngine
{
    private static readonly Lazy<bool> _isAvailable = new(Probe);

    /// <summary>
    /// Gets a value indicating whether the native library loaded and can construct an image.
    /// </summary>
    internal static bool IsAvailable => _isAvailable.Value;

    private static bool Probe()
    {
        try
        {
            using var image = new MagickImage(MagickColors.White, 1, 1);
            return image.Width == 1 && image.Height == 1;
        }
#pragma warning disable CA1031 // A missing or broken native library is exactly the case this probe exists for.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }
}
