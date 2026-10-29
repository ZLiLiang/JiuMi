namespace JiuMi.Application.ImageConversion;

/// <summary>
/// The image formats the conversion tool reads and writes.
/// </summary>
public enum ImageFormat
{
    /// <summary>Portable Network Graphics.</summary>
    Png,

    /// <summary>JPEG (the <c>.jpg</c> and <c>.jpeg</c> extensions both map here).</summary>
    Jpeg,

    /// <summary>WebP.</summary>
    WebP,

    /// <summary>Windows bitmap; source only.</summary>
    Bmp,

    /// <summary>Photoshop document; source only, read through its embedded composite.</summary>
    Psd,

    /// <summary>Large Photoshop document; source only, read through its embedded composite.</summary>
    Psb,
}
