namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Formats the duration, size and second line shown by the WeChat video list.
/// </summary>
public static class WeChatVideoText
{
    private const long BytesPerKilobyte = 1024;
    private const long BytesPerMegabyte = BytesPerKilobyte * 1024;
    private const long BytesPerGigabyte = BytesPerMegabyte * 1024;

    /// <summary>
    /// The separator between the author, duration and size.
    /// </summary>
    public const string MetaSeparator = " · ";

    /// <summary>
    /// Formats a playback length as <c>m:ss</c> or <c>h:mm:ss</c>.
    /// </summary>
    /// <param name="seconds">The playback length in seconds.</param>
    /// <returns>The formatted length, or an empty string when it is unknown.</returns>
    public static string FormatDuration(int? seconds)
    {
        if (seconds is not > 0)
        {
            return string.Empty;
        }

        TimeSpan value = TimeSpan.FromSeconds(seconds.Value);
        return value.TotalHours >= 1
            ? FormattableString.Invariant($"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}")
            : FormattableString.Invariant($"{value.Minutes}:{value.Seconds:00}");
    }

    /// <summary>
    /// Formats a byte count with B, KB, MB or GB, using at most one decimal place.
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The formatted size, or an empty string when it is unknown.</returns>
    public static string FormatSize(long? bytes)
    {
        if (bytes is not > 0)
        {
            return string.Empty;
        }

        long value = bytes.Value;
        if (value >= BytesPerGigabyte)
        {
            return FormattableString.Invariant($"{value / (double)BytesPerGigabyte:0.#} GB");
        }

        if (value >= BytesPerMegabyte)
        {
            return FormattableString.Invariant($"{value / (double)BytesPerMegabyte:0.#} MB");
        }

        return value >= BytesPerKilobyte
            ? FormattableString.Invariant($"{value / (double)BytesPerKilobyte:0.#} KB")
            : FormattableString.Invariant($"{value} B");
    }

    /// <summary>
    /// Builds the second line of a list row from the parts that are known.
    /// </summary>
    /// <param name="uploader">The uploader nickname.</param>
    /// <param name="seconds">The playback length in seconds.</param>
    /// <param name="bytes">The actual size in bytes.</param>
    /// <returns>The joined parts, or an empty string when none is known.</returns>
    public static string BuildMeta(string? uploader, int? seconds, long? bytes)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(uploader))
        {
            parts.Add(uploader.Trim());
        }

        string duration = FormatDuration(seconds);
        if (duration.Length > 0)
        {
            parts.Add(duration);
        }

        string size = FormatSize(bytes);
        if (size.Length > 0)
        {
            parts.Add(size);
        }

        return string.Join(MetaSeparator, parts);
    }
}
