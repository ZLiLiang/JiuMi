using System.Globalization;

namespace JiuMi.Application.ImageConversion;

/// <summary>
/// A colour without alpha, stored as the three channels the JPG background needs.
/// </summary>
public readonly record struct ImageRgbColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Gets white, the default background of JPEG output.
    /// </summary>
    public static ImageRgbColor White => new(0xFF, 0xFF, 0xFF);

    /// <summary>
    /// Gets black.
    /// </summary>
    public static ImageRgbColor Black => new(0, 0, 0);

    /// <summary>
    /// Formats the colour as <c>#RRGGBB</c> for settings and the interface.
    /// </summary>
    /// <returns>The upper-case hexadecimal form.</returns>
    public string ToHex() => string.Create(
        CultureInfo.InvariantCulture,
        $"#{R:X2}{G:X2}{B:X2}");

    /// <summary>
    /// Parses <c>#RRGGBB</c> or <c>RRGGBB</c>, ignoring case and surrounding white space.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="color">The parsed colour when the text is valid.</param>
    /// <returns><see langword="true"/> when the text was a six-digit hexadecimal colour.</returns>
    public static bool TryParse(string? text, out ImageRgbColor color)
    {
        color = White;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length != 6 ||
            !byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(span[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        color = new ImageRgbColor(r, g, b);
        return true;
    }
}
