using ImageMagick;
using JiuMi.Application.ImageConversion;

namespace JiuMi.Infrastructure.ImageConversion;

/// <summary>
/// The decoded source image a probe keeps alive until the row is converted, so the file is decoded
/// once for both the thumbnail and the conversion.
/// </summary>
internal sealed class MagickDecodedImage : IDecodedImageHandle
{
    private MagickImage? _image;

    internal MagickDecodedImage(MagickImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _image = image;
    }

    /// <summary>
    /// Creates a copy the converter may mutate without changing the cached image.
    /// </summary>
    /// <returns>The copy, owned by the caller.</returns>
    internal MagickImage CreateCopy()
    {
        MagickImage image = _image ?? throw new ObjectDisposedException(nameof(MagickDecodedImage));
        return new MagickImage(image);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _image?.Dispose();
        _image = null;
    }
}
