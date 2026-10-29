namespace JiuMi.Application.ImageConversion;

/// <summary>
/// An opaque decoded source image that a probe hands to the converter, so a file is decoded once for
/// both the thumbnail and the conversion.
/// </summary>
public interface IDecodedImageHandle : IDisposable
{
}
