using System.Security;
using ImageMagick;
using JiuMi.Application.ImageConversion;

namespace JiuMi.Infrastructure.ImageConversion;

/// <summary>
/// Converts sources with Magick.NET: one engine for decoding, colour conversion, scaling and encoding.
/// </summary>
public sealed class MagickImageConverter : IImageConverter
{
    /// <inheritdoc />
    public bool IsAvailable => MagickEngine.IsAvailable;

    /// <inheritdoc />
    public Task<ImageConversionResult> ConvertAsync(
        ImageProbeResult probe,
        ImageConversionRequest request,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return Task.Run(() => Convert(probe, request, outputPath, cancellationToken), cancellationToken);
    }

    private static ImageConversionResult Convert(
        ImageProbeResult probe,
        ImageConversionRequest request,
        string outputPath,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ImageConversionValidationError> errors = ImageConversionRules.Validate(request);
        if (errors.Count > 0)
        {
            ImageConversionFailureReason reason = errors.Contains(ImageConversionValidationError.TargetFormatNotSupported)
                ? ImageConversionFailureReason.UnsupportedFormat
                : ImageConversionFailureReason.EncodeFailed;
            return ImageConversionResult.Failed(probe.SourcePath, reason);
        }

        if (probe.Failure is { } probeFailure)
        {
            return ImageConversionResult.Failed(probe.SourcePath, probeFailure, probe.Error);
        }

        if (File.Exists(outputPath))
        {
            return ImageConversionResult.Failed(
                probe.SourcePath,
                ImageConversionFailureReason.WriteFailed,
                new IOException($"The output file already exists: {outputPath}"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        MagickImage? image = null;
        try
        {
            image = probe.Decoded is MagickDecodedImage decoded
                ? decoded.CreateCopy()
                : new MagickImage(probe.SourcePath);

            image.AutoOrient();
            EnsureSrgb(image);

            if (!request.Advanced.KeepSourceMetadata)
            {
                image.Strip();
            }

            image.SetProfile(ColorProfiles.SRGB);
            Resize(image, request.MaxEdge);
            FlattenForJpeg(image, request);
            Encode(image, request);
            cancellationToken.ThrowIfCancellationRequested();
            Write(image, outputPath);
            return ImageConversionResult.Success(probe.SourcePath, outputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ImageConversionResult.Failed(probe.SourcePath, Classify(exception), exception);
        }
        finally
        {
            image?.Dispose();
        }
    }

    private static void EnsureSrgb(MagickImage image)
    {
        IColorProfile? profile = image.GetColorProfile();
        if (profile is not null)
        {
            if (profile.ColorSpace != ColorSpace.sRGB &&
                !image.TransformColorSpace(ColorProfiles.SRGB))
            {
                image.ColorSpace = ColorSpace.sRGB;
            }

            return;
        }

        if (image.ColorSpace != ColorSpace.sRGB)
        {
            image.ColorSpace = ColorSpace.sRGB;
        }
    }

    private static void Resize(MagickImage image, int? maxEdge)
    {
        if (maxEdge is not { } edge || Math.Max(image.Width, image.Height) <= edge)
        {
            return;
        }

        image.Resize(new MagickGeometry((uint)edge, (uint)edge) { Greater = true });
    }

    private static void FlattenForJpeg(MagickImage image, ImageConversionRequest request)
    {
        if (request.TargetFormat != ImageFormat.Jpeg || !image.HasAlpha)
        {
            return;
        }

        ImageRgbColor background = request.JpegBackgroundColor;
        image.BackgroundColor = MagickColor.FromRgb(background.R, background.G, background.B);
        image.Alpha(AlphaAction.Remove);
    }

    private static void Encode(MagickImage image, ImageConversionRequest request)
    {
        switch (request.TargetFormat)
        {
            case ImageFormat.Png:
                image.Format = MagickFormat.Png;
                image.Settings.SetDefine(
                    MagickFormat.Png,
                    "compression-level",
                    request.Advanced.PngCompressionLevel);

                // Strip() leaves an image artifact that excludes the iCCP chunk and lets ImageMagick
                // write the PNG sRGB chunk instead. Excluding that chunk keeps the embedded profile.
                image.SetArtifact("png:exclude-chunk", "sRGB");
                break;

            case ImageFormat.Jpeg:
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)request.Quality;
                image.Depth = 8;
                break;

            case ImageFormat.WebP:
                image.Format = MagickFormat.WebP;
                image.Quality = (uint)request.Quality;
                image.Depth = 8;
                image.Settings.SetDefine(MagickFormat.WebP, "lossless", request.Advanced.WebPLossless);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.TargetFormat,
                    "Not an output format.");
        }
    }

    private static void Write(MagickImage image, string outputPath)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        image.Write(outputPath);
    }

    private static ImageConversionFailureReason Classify(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is IOException or UnauthorizedAccessException or SecurityException)
            {
                return ImageConversionFailureReason.WriteFailed;
            }
        }

        return ImageConversionFailureReason.EncodeFailed;
    }
}
