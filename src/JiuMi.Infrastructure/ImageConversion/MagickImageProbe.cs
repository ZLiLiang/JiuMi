using ImageMagick;
using JiuMi.Application.ImageConversion;

namespace JiuMi.Infrastructure.ImageConversion;

/// <summary>
/// Probes sources with Magick.NET: one read yields the composite size, the thumbnail and the image
/// the converter reuses.
/// </summary>
public sealed class MagickImageProbe : IImageProbe
{
    /// <inheritdoc />
    public bool IsAvailable => MagickEngine.IsAvailable;

    /// <inheritdoc />
    public Task<ImageProbeResult> ProbeAsync(
        string sourcePath,
        int thumbnailMaxEdge,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thumbnailMaxEdge);

        return Task.Run(() => Probe(sourcePath, thumbnailMaxEdge, cancellationToken), cancellationToken);
    }

    private static ImageProbeResult Probe(string sourcePath, int thumbnailMaxEdge, CancellationToken cancellationToken)
    {
        if (!ImageFormatCatalog.TryGetSourceFormat(sourcePath, out ImageFormat format))
        {
            return ImageProbeResult.Failed(sourcePath, format: null, ImageConversionFailureReason.UnsupportedFormat);
        }

        cancellationToken.ThrowIfCancellationRequested();

        MagickImage? image = null;
        try
        {
            image = new MagickImage(sourcePath);
            if (image.Width == 0 || image.Height == 0)
            {
                image.Dispose();
                return ImageProbeResult.Failed(sourcePath, format, ImageConversionFailureReason.DecodeFailed);
            }

            image.AutoOrient();
            int width = (int)image.Width;
            int height = (int)image.Height;
            byte[] thumbnail = CreateThumbnail(image, thumbnailMaxEdge);
            cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable CA2000 // The result takes the decoded image over and disposes it with the row.
            var decoded = new MagickDecodedImage(image);
#pragma warning restore CA2000
            image = null;
            return ImageProbeResult.Success(sourcePath, format, width, height, thumbnail, decoded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            image?.Dispose();
            return ImageProbeResult.Failed(
                sourcePath,
                format,
                ImageConversionFailureReason.DecodeFailed,
                exception);
        }
    }

    private static byte[] CreateThumbnail(MagickImage source, int maxEdge)
    {
        using var thumbnail = new MagickImage(source);
        if (Math.Max(thumbnail.Width, thumbnail.Height) > maxEdge)
        {
            thumbnail.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge) { Greater = true });
        }

        thumbnail.Format = MagickFormat.Png;
        using var stream = new MemoryStream();
        thumbnail.Write(stream);
        return stream.ToArray();
    }
}
