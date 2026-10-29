using JiuMi.Application.ImageConversion;

namespace JiuMi.Application.Tests;

public sealed class ImageFormatCatalogTests
{
    [Theory]
    [InlineData(".png", ImageFormat.Png)]
    [InlineData(".PNG", ImageFormat.Png)]
    [InlineData(".jpg", ImageFormat.Jpeg)]
    [InlineData(".JPG", ImageFormat.Jpeg)]
    [InlineData(".jpeg", ImageFormat.Jpeg)]
    [InlineData(".webp", ImageFormat.WebP)]
    [InlineData(".bmp", ImageFormat.Bmp)]
    [InlineData(".psd", ImageFormat.Psd)]
    [InlineData(".psb", ImageFormat.Psb)]
    public void TryGetFormat_GivenKnownExtension_ResolvesFormat(string extension, ImageFormat expected)
    {
        bool resolved = ImageFormatCatalog.TryGetFormat(extension, out ImageFormat format);

        Assert.True(resolved);
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData(".gif")]
    [InlineData(".tif")]
    [InlineData("")]
    [InlineData(".png.gz")]
    public void TryGetFormat_GivenUnknownExtension_ReturnsFalse(string extension)
    {
        Assert.False(ImageFormatCatalog.TryGetFormat(extension, out _));
    }

    [Theory]
    [InlineData(@"C:\assets\Icon.PSD", ImageFormat.Psd)]
    [InlineData(@"C:\assets\photo.JPEG", ImageFormat.Jpeg)]
    [InlineData("photo.webp", ImageFormat.WebP)]
    public void TryGetSourceFormat_GivenSupportedPath_ResolvesFormat(string path, ImageFormat expected)
    {
        Assert.True(ImageFormatCatalog.TryGetSourceFormat(path, out ImageFormat format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData(@"C:\assets\notes.txt")]
    [InlineData(@"C:\assets\archive.zip")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetSourceFormat_GivenUnsupportedPath_ReturnsFalse(string path)
    {
        Assert.False(ImageFormatCatalog.TryGetSourceFormat(path, out _));
    }

    [Fact]
    public void OutputFormats_ArePngJpegWebPInOrder()
    {
        Assert.Equal(
            [ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.WebP],
            ImageFormatCatalog.OutputFormats);
    }

    [Theory]
    [InlineData(ImageFormat.Png, true)]
    [InlineData(ImageFormat.WebP, true)]
    [InlineData(ImageFormat.Bmp, true)]
    [InlineData(ImageFormat.Psd, true)]
    [InlineData(ImageFormat.Jpeg, false)]
    public void SupportsTransparency_IsFalseOnlyForJpeg(ImageFormat format, bool expected)
    {
        Assert.Equal(expected, ImageFormatCatalog.SupportsTransparency(format));
    }

    [Theory]
    [InlineData(ImageFormat.Png, false)]
    [InlineData(ImageFormat.Jpeg, true)]
    [InlineData(ImageFormat.WebP, true)]
    [InlineData(ImageFormat.Bmp, false)]
    public void IsLossy_IsTrueForJpegAndWebP(ImageFormat format, bool expected)
    {
        Assert.Equal(expected, ImageFormatCatalog.IsLossy(format));
    }

    [Theory]
    [InlineData(ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.WebP, true)]
    [InlineData(ImageFormat.Bmp, ImageFormat.Psd, ImageFormat.Psb, false)]
    public void IsOutputFormat_AcceptsOnlyTargets(
        ImageFormat first,
        ImageFormat second,
        ImageFormat third,
        bool expected)
    {
        Assert.Equal(expected, ImageFormatCatalog.IsOutputFormat(first));
        Assert.Equal(expected, ImageFormatCatalog.IsOutputFormat(second));
        Assert.Equal(expected, ImageFormatCatalog.IsOutputFormat(third));
    }

    [Theory]
    [InlineData(ImageFormat.Png, ".png")]
    [InlineData(ImageFormat.Jpeg, ".jpg")]
    [InlineData(ImageFormat.WebP, ".webp")]
    public void GetExtension_ReturnsOutputExtension(ImageFormat format, string expected)
    {
        Assert.Equal(expected, ImageFormatCatalog.GetExtension(format));
    }

    [Theory]
    [InlineData(ImageFormat.Png, "PNG")]
    [InlineData(ImageFormat.Jpeg, "JPEG")]
    [InlineData(ImageFormat.WebP, "WebP")]
    [InlineData(ImageFormat.Bmp, "BMP")]
    [InlineData(ImageFormat.Psd, "PSD")]
    [InlineData(ImageFormat.Psb, "PSB")]
    public void GetDisplayName_ReturnsShortName(ImageFormat format, string expected)
    {
        Assert.Equal(expected, ImageFormatCatalog.GetDisplayName(format));
    }
}
