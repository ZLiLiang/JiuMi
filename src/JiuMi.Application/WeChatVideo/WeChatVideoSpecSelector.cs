using System.Globalization;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Normalizes, sorts, deduplicates and formats <see cref="WeChatVideoSpec"/> variants for user selection.
/// Always places the default stream first so the user explicitly opts into alternative tiers.
/// </summary>
public static class WeChatVideoSpecSelector
{
    /// <summary>
    /// Builds the ordered list of selectable quality options, starting with the default stream option
    /// followed by deduplicated variants sorted by resolution descending and bitrate descending.
    /// </summary>
    /// <param name="specs">The raw <c>spec</c> entries captured from WeChat metadata.</param>
    /// <param name="defaultLabel">The localized display label for the default option.</param>
    /// <returns>The ordered options, always containing at least the default option.</returns>
    public static IReadOnlyList<WeChatVideoSpecOption> BuildOptions(
        IReadOnlyList<WeChatVideoSpec> specs,
        string defaultLabel)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLabel);

        var options = new List<WeChatVideoSpecOption>
        {
            new()
            {
                FileFormat = null,
                Label = defaultLabel,
            },
        };

        var candidates = new List<NormalizedSpec>();
        foreach (WeChatVideoSpec spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.FileFormat))
            {
                continue;
            }

            string fileFormat = spec.FileFormat.Trim();
            int? width = spec.Width is > 0 ? spec.Width : null;
            int? height = spec.Height is > 0 ? spec.Height : null;
            long area = width is not null && height is not null
                ? (long)width.Value * height.Value
                : 0L;
            int? effectiveBitRate = spec.VideoBitRate is > 0
                ? spec.VideoBitRate
                : spec.BitRate is > 0
                    ? spec.BitRate
                    : null;
            string? codingFormat = NormalizeCodingFormat(spec.CodingFormat);

            candidates.Add(new NormalizedSpec(
                fileFormat,
                width,
                height,
                area,
                effectiveBitRate,
                codingFormat));
        }

        candidates.Sort(CompareCandidates);

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (NormalizedSpec candidate in candidates)
        {
            string dedupeKey = CreateDedupeKey(candidate);
            if (!seenKeys.Add(dedupeKey))
            {
                continue;
            }

            options.Add(new WeChatVideoSpecOption
            {
                FileFormat = candidate.FileFormat,
                Label = FormatLabel(
                    candidate.Width,
                    candidate.Height,
                    candidate.CodingFormat,
                    candidate.FileFormat),
                Width = candidate.Width,
                Height = candidate.Height,
                CodingFormat = candidate.CodingFormat,
                EffectiveBitRate = candidate.EffectiveBitRate,
            });
        }

        return options;
    }

    /// <summary>
    /// Normalizes a raw codec identifier (such as <c>h265</c> or <c>h264</c>) into a concise label.
    /// </summary>
    /// <param name="codingFormat">The raw codec string from WeChat metadata.</param>
    /// <returns>The normalized display label, or <see langword="null"/> when empty.</returns>
    public static string? NormalizeCodingFormat(string? codingFormat)
    {
        if (string.IsNullOrWhiteSpace(codingFormat))
        {
            return null;
        }

        string trimmed = codingFormat.Trim();
        if (string.Equals(trimmed, "h265", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "hevc", StringComparison.OrdinalIgnoreCase))
        {
            return "H.265";
        }

        if (string.Equals(trimmed, "h264", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "avc", StringComparison.OrdinalIgnoreCase))
        {
            return "H.264";
        }

        if (string.Equals(trimmed, "av1", StringComparison.OrdinalIgnoreCase))
        {
            return "AV1";
        }

        if (string.Equals(trimmed, "vp9", StringComparison.OrdinalIgnoreCase))
        {
            return "VP9";
        }

        return trimmed.ToUpperInvariant();
    }

    private static int CompareCandidates(NormalizedSpec left, NormalizedSpec right)
    {
        int areaComparison = right.Area.CompareTo(left.Area);
        if (areaComparison != 0)
        {
            return areaComparison;
        }

        int bitRateComparison = (right.EffectiveBitRate ?? 0).CompareTo(left.EffectiveBitRate ?? 0);
        if (bitRateComparison != 0)
        {
            return bitRateComparison;
        }

        return string.CompareOrdinal(left.FileFormat, right.FileFormat);
    }

    private static string CreateDedupeKey(NormalizedSpec candidate)
    {
        if (candidate.Width is not null && candidate.Height is not null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"dim:{candidate.Width.Value}x{candidate.Height.Value}:{candidate.CodingFormat ?? string.Empty}");
        }

        return "fmt:" + candidate.FileFormat;
    }

    private static string FormatLabel(int? width, int? height, string? codingFormat, string fileFormat)
    {
        if (width is not null && height is not null)
        {
            int shortEdge = Math.Min(width.Value, height.Value);
            string resolution = string.Create(CultureInfo.InvariantCulture, $"{shortEdge}p");
            return codingFormat is not null
                ? string.Concat(resolution, " · ", codingFormat)
                : resolution;
        }

        return codingFormat is not null
            ? string.Concat(fileFormat, " · ", codingFormat)
            : fileFormat;
    }

    private sealed record NormalizedSpec(
        string FileFormat,
        int? Width,
        int? Height,
        long Area,
        int? EffectiveBitRate,
        string? CodingFormat);
}
