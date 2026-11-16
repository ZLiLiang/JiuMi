namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The cover image bytes, or a placeholder marker when the image could not be fetched.
/// </summary>
public sealed record WeChatVideoThumbnailResult
{
    /// <summary>
    /// Gets a value indicating whether the caller should show its placeholder image.
    /// </summary>
    public required bool IsPlaceholder
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the image bytes; empty when <see cref="IsPlaceholder"/> is <see langword="true"/>.
    /// </summary>
    public required byte[] Bytes
    {
        get;
        init;
    }

    /// <summary>
    /// Creates a fetched image.
    /// </summary>
    /// <param name="bytes">The image bytes.</param>
    /// <returns>The result.</returns>
    public static WeChatVideoThumbnailResult Success(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            throw new ArgumentException("An image must have bytes.", nameof(bytes));
        }

        return new WeChatVideoThumbnailResult
        {
            IsPlaceholder = false,
            Bytes = bytes,
        };
    }

    /// <summary>
    /// Creates the placeholder result used when the image could not be fetched.
    /// </summary>
    /// <returns>The result.</returns>
    public static WeChatVideoThumbnailResult Placeholder() =>
        new()
        {
            IsPlaceholder = true,
            Bytes = [],
        };
}
