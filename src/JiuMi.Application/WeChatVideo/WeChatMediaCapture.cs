namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// A complete media response observed while WeChat loads a video.
/// </summary>
/// <param name="Url">The full media URL, including its session token.</param>
/// <param name="TotalBytes">The total media size from the response range.</param>
/// <param name="CapturedAt">When the response was observed.</param>
public sealed record WeChatMediaCapture(Uri Url, long TotalBytes, DateTimeOffset CapturedAt);
