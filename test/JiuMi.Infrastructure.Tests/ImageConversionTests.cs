using ImageMagick;
using JiuMi.Application.ImageConversion;
using JiuMi.Infrastructure.ImageConversion;

namespace JiuMi.Infrastructure.Tests;

public sealed class ImageConversionTests : IDisposable
{
    private const int ThumbnailMaxEdge = 256;

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.ImageConversion.Tests." + Guid.NewGuid().ToString("N"));

    private readonly MagickImageProbe _probe = new();
    private readonly MagickImageConverter _converter = new();

    public ImageConversionTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ProbeAsync_GivenPsd_ReadsCompositeSizeAndThumbnail()
    {
        string path = CreateImage("icon.psd", MagickFormat.Psd, 40, 30);

        using ImageProbeResult result = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        Assert.True(result.IsDecodable);
        Assert.Equal(ImageFormat.Psd, result.Format);
        Assert.Equal(40, result.PixelWidth);
        Assert.Equal(30, result.PixelHeight);
        Assert.NotNull(result.ThumbnailPng);
        using var thumbnail = new MagickImage(result.ThumbnailPng);
        Assert.Equal(40u, thumbnail.Width);
        Assert.Equal(30u, thumbnail.Height);
    }

    [Fact]
    public async Task ProbeAsync_GivenPsb_ReadsCompositeSize()
    {
        string path = CreateImage("large.psb", MagickFormat.Psb, 64, 48);

        using ImageProbeResult result = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        Assert.True(result.IsDecodable);
        Assert.Equal(ImageFormat.Psb, result.Format);
        Assert.Equal(64, result.PixelWidth);
        Assert.Equal(48, result.PixelHeight);
    }

    [Fact]
    public async Task ProbeAsync_GivenLargeImage_ShrinksThumbnailToTheLimit()
    {
        string path = CreateImage("wide.png", MagickFormat.Png, 800, 400);

        using ImageProbeResult result = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        Assert.NotNull(result.ThumbnailPng);
        using var thumbnail = new MagickImage(result.ThumbnailPng);
        Assert.Equal(256u, thumbnail.Width);
        Assert.Equal(128u, thumbnail.Height);
    }

    [Fact]
    public async Task ProbeAsync_GivenUnsupportedExtension_ReturnsUnsupportedFormat()
    {
        string path = Path.Combine(_root, "notes.txt");
        await File.WriteAllTextAsync(path, "not an image");

        using ImageProbeResult result = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        Assert.False(result.IsDecodable);
        Assert.Equal(ImageConversionFailureReason.UnsupportedFormat, result.Failure);
        Assert.Null(result.Decoded);
    }

    [Fact]
    public async Task ProbeAsync_GivenCorruptPsd_ReturnsDecodeFailedInsteadOfThrowing()
    {
        string path = Path.Combine(_root, "broken.psd");
        await File.WriteAllTextAsync(path, "not a photoshop document");

        using ImageProbeResult result = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        Assert.False(result.IsDecodable);
        Assert.Equal(ImageConversionFailureReason.DecodeFailed, result.Failure);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData(ImageFormat.Png, MagickFormat.Png)]
    [InlineData(ImageFormat.Jpeg, MagickFormat.Jpeg)]
    [InlineData(ImageFormat.WebP, MagickFormat.WebP)]
    public async Task ConvertAsync_GivenEachTargetFormat_WritesThatFormat(
        ImageFormat target,
        MagickFormat expected)
    {
        string path = CreateImage("source.png", MagickFormat.Png, 32, 24);
        string output = OutputPath("source", target);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = target },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Equal(expected, written.Format);
    }

    [Fact]
    public async Task ConvertAsync_ReusesTheProbedImageWhenTheSourceIsGone()
    {
        string path = CreateImage("delete-me.psd", MagickFormat.Psd, 24, 16);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);
        File.Delete(path);
        string output = OutputPath("delete-me", ImageFormat.Png);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task ConvertAsync_EmbedsSRgbProfile()
    {
        string path = CreateImage("colour.png", MagickFormat.Png, 16, 16);
        string output = OutputPath("colour", ImageFormat.Png);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Equal(ColorSpace.sRGB, written.ColorSpace);
        Assert.Equal(ColorSpace.sRGB, written.GetColorProfile()?.ColorSpace);
    }

    [Fact]
    public async Task ConvertAsync_StripsExifByDefault()
    {
        string path = CreateJpegWithExif("camera.jpg");
        string output = OutputPath("camera", ImageFormat.Png);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Null(written.GetExifProfile());
    }

    [Fact]
    public async Task ConvertAsync_GivenKeepSourceMetadata_KeepsExif()
    {
        string path = CreateJpegWithExif("camera.jpg");
        string output = OutputPath("camera", ImageFormat.Jpeg);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest
            {
                TargetFormat = ImageFormat.Jpeg,
                Advanced = new ImageConversionAdvancedOptions { KeepSourceMetadata = true },
            },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.NotNull(written.GetExifProfile());
        Assert.Equal("JiuMi", written.GetExifProfile()?.GetValue(ExifTag.Make)?.Value);
    }

    [Fact]
    public async Task ConvertAsync_GivenTransparentPngToJpeg_UsesTheBackgroundColour()
    {
        string path = CreateTransparentPng("alpha.png", 8, 8);
        string output = OutputPath("alpha", ImageFormat.Jpeg);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest
            {
                TargetFormat = ImageFormat.Jpeg,
                JpegBackgroundColor = new ImageRgbColor(0x12, 0xA0, 0x40),
            },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.False(written.HasAlpha);
        IMagickColor<ushort>? pixel = written.GetPixels().GetPixel(0, 0).ToColor();
        Assert.NotNull(pixel);
        Assert.InRange(pixel.R / 257.0, 0x12 - 10, 0x12 + 10);
        Assert.InRange(pixel.G / 257.0, 0xA0 - 10, 0xA0 + 10);
        Assert.InRange(pixel.B / 257.0, 0x40 - 10, 0x40 + 10);
    }

    [Fact]
    public async Task ConvertAsync_GivenSmallerMaxEdge_ScalesProportionally()
    {
        string path = CreateImage("big.png", MagickFormat.Png, 200, 100);
        string output = OutputPath("big", ImageFormat.Png);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png, MaxEdge = 80 },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Equal(80u, written.Width);
        Assert.Equal(40u, written.Height);
    }

    [Fact]
    public async Task ConvertAsync_GivenLargerMaxEdge_DoesNotEnlarge()
    {
        string path = CreateImage("small.png", MagickFormat.Png, 40, 20);
        string output = OutputPath("small", ImageFormat.Png);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png, MaxEdge = 200 },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Equal(40u, written.Width);
        Assert.Equal(20u, written.Height);
    }

    [Fact]
    public async Task ConvertAsync_GivenExistingOutput_RefusesToOverwrite()
    {
        string path = CreateImage("source.png", MagickFormat.Png, 12, 12);
        string output = OutputPath("source", ImageFormat.Png);
        await File.WriteAllTextAsync(output, "keep me");
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            output);

        Assert.False(result.Succeeded);
        Assert.Equal(ImageConversionFailureReason.WriteFailed, result.Failure);
        Assert.Equal("keep me", await File.ReadAllTextAsync(output));
    }

    [Fact]
    public async Task ConvertAsync_GivenFailedProbe_ReturnsTheSameReason()
    {
        string path = Path.Combine(_root, "broken.psd");
        await File.WriteAllTextAsync(path, "not a photoshop document");
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            OutputPath("broken", ImageFormat.Png));

        Assert.False(result.Succeeded);
        Assert.Equal(ImageConversionFailureReason.DecodeFailed, result.Failure);
    }

    [Fact]
    public async Task ConvertAsync_Given16BitSource_Keeps16BitPngOutput()
    {
        string path = Path.Combine(_root, "deep.png");
        using (var image = new MagickImage(MagickColors.Black, 16, 16))
        {
            image.GetPixels().SetPixel(0, 0, [0x1234, 0x5678, 0x9ABC]);
            image.Format = MagickFormat.Png;
            image.Write(path);
        }

        string output = OutputPath("deep", ImageFormat.Png);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Equal(16u, written.Depth);
    }

    [Fact]
    public async Task ConvertAsync_GivenBmpInput_WritesPng()
    {
        string path = CreateImage("bitmap.bmp", MagickFormat.Bmp, 20, 12);
        string output = OutputPath("bitmap", ImageFormat.Png);
        using ImageProbeResult probe = await _probe.ProbeAsync(path, ThumbnailMaxEdge);

        ImageConversionResult result = await _converter.ConvertAsync(
            probe,
            new ImageConversionRequest { TargetFormat = ImageFormat.Png },
            output);

        Assert.True(result.Succeeded, result.Error?.Message);
        using var written = new MagickImage(output);
        Assert.Equal(MagickFormat.Png, written.Format);
    }

    private string CreateImage(string name, MagickFormat format, uint width, uint height)
    {
        string path = Path.Combine(_root, name);
        using var image = new MagickImage(MagickColors.Red, width, height);
        image.Format = format;
        image.Write(path);
        return path;
    }

    private string CreateTransparentPng(string name, uint width, uint height)
    {
        string path = Path.Combine(_root, name);
        using var image = new MagickImage(MagickColors.Transparent, width, height);
        image.Format = MagickFormat.Png;
        image.Write(path);
        return path;
    }

    private string CreateJpegWithExif(string name)
    {
        string path = Path.Combine(_root, name);
        using var image = new MagickImage(MagickColors.Blue, 24, 18);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "JiuMi");
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        image.Write(path);
        return path;
    }

    private string OutputPath(string name, ImageFormat format) =>
        Path.Combine(_root, "converted-" + name + ImageFormatCatalog.GetExtension(format));
}
