namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// The bytes written so far and the size the server reported.
/// </summary>
/// <param name="BytesReceived">The number of bytes written to the partial file.</param>
/// <param name="TotalBytes">The total size from the server headers.</param>
public readonly record struct WeChatVideoDownloadProgress(long BytesReceived, long TotalBytes);
