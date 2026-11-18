namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The outcome of one download attempt.
/// </summary>
public sealed record WeChatVideoDownloadResult
{
    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public required WeChatVideoDownloadStatus Status
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the finished file path, when the download completed.
    /// </summary>
    public string? FilePath
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the bytes written before the attempt ended.
    /// </summary>
    public long BytesReceived
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the total size the server reported, when known.
    /// </summary>
    public long? TotalBytes
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the failure category, or <see cref="WeChatVideoDownloadFailure.None"/> when the status
    /// already explains the outcome.
    /// </summary>
    public WeChatVideoDownloadFailure Failure
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the exception behind a failure, for logging.
    /// </summary>
    public Exception? Error
    {
        get;
        init;
    }

    /// <summary>
    /// Gets a value indicating whether the file was written.
    /// </summary>
    public bool Succeeded => Status == WeChatVideoDownloadStatus.Completed;

    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    /// <param name="filePath">The finished file.</param>
    /// <param name="bytesReceived">The bytes written.</param>
    /// <param name="totalBytes">The total size.</param>
    /// <returns>The outcome.</returns>
    public static WeChatVideoDownloadResult Success(
        string filePath,
        long bytesReceived,
        long totalBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new WeChatVideoDownloadResult
        {
            Status = WeChatVideoDownloadStatus.Completed,
            FilePath = filePath,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes,
        };
    }

    /// <summary>
    /// Creates an expired-link outcome.
    /// </summary>
    /// <param name="bytesReceived">The bytes written before the link expired.</param>
    /// <param name="totalBytes">The total size, when known.</param>
    /// <returns>The outcome.</returns>
    public static WeChatVideoDownloadResult Expired(long bytesReceived = 0, long? totalBytes = null) =>
        new()
        {
            Status = WeChatVideoDownloadStatus.Expired,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes,
        };

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="failure">Why it failed.</param>
    /// <param name="bytesReceived">The bytes written before the failure.</param>
    /// <param name="totalBytes">The total size, when known.</param>
    /// <param name="error">The exception, for logging.</param>
    /// <returns>The outcome.</returns>
    public static WeChatVideoDownloadResult Failed(
        WeChatVideoDownloadFailure failure,
        long bytesReceived = 0,
        long? totalBytes = null,
        Exception? error = null)
    {
        if (failure == WeChatVideoDownloadFailure.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failure), "A failure must name its reason.");
        }

        return new WeChatVideoDownloadResult
        {
            Status = WeChatVideoDownloadStatus.Failed,
            Failure = failure,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes,
            Error = error,
        };
    }

    /// <summary>
    /// Creates a canceled outcome.
    /// </summary>
    /// <param name="bytesReceived">The bytes written before the cancellation.</param>
    /// <param name="totalBytes">The total size, when known.</param>
    /// <returns>The outcome.</returns>
    public static WeChatVideoDownloadResult Canceled(long bytesReceived = 0, long? totalBytes = null) =>
        new()
        {
            Status = WeChatVideoDownloadStatus.Canceled,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes,
        };
}
