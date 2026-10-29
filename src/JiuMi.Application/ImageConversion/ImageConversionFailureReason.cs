namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Why one file did not produce an output. The declaration order is the fixed priority used when a
/// row reports more than one reason.
/// </summary>
public enum ImageConversionFailureReason
{
    /// <summary>The extension is not a supported input.</summary>
    UnsupportedFormat,

    /// <summary>The source could not be decoded, for example a PSD without a saved composite.</summary>
    DecodeFailed,

    /// <summary>The target format could not be encoded.</summary>
    EncodeFailed,

    /// <summary>The output could not be written: no permission, missing directory or a full disk.</summary>
    WriteFailed,
}
