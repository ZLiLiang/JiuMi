using System.Globalization;
using JiuMi.Application.FileNames;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Turns a WeChat video description into a Windows-safe file stem and keeps downloads from
/// overwriting an existing final or partial file.
/// </summary>
public static class WeChatVideoFileName
{
    /// <summary>The longest stem the cleaner produces.</summary>
    public const int MaxStemLength = 120;

    /// <summary>The extension of a finished video.</summary>
    public const string Extension = ".mp4";

    /// <summary>The extension of a video still being written.</summary>
    public const string PartExtension = ".mp4.part";

    private const string FallbackPrefix = "视频_";

    private static readonly char[] _illegalCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// Cleans a description for use as a file stem.
    /// </summary>
    /// <param name="description">The description WeChat reported, when any.</param>
    /// <param name="capturedAt">The capture time used when the description is empty.</param>
    /// <returns>A non-empty stem of at most <see cref="MaxStemLength"/> characters.</returns>
    public static string CreateStem(string? description, DateTimeOffset capturedAt)
    {
        string name = ReplaceIllegal(description ?? string.Empty).Trim().TrimEnd(' ', '.');
        if (name.Length == 0)
        {
            name = CreateFallback(capturedAt);
        }

        if (FileNameRules.IsReservedName(name))
        {
            name = "_" + name;
        }

        if (name.Length > MaxStemLength)
        {
            name = Truncate(name, MaxStemLength).TrimEnd(' ', '.');
            if (name.Length == 0)
            {
                name = CreateFallback(capturedAt);
            }
        }

        return name;
    }

    /// <summary>
    /// Appends <c>-1</c>, <c>-2</c>, ... until neither the final file nor its partial file exists.
    /// </summary>
    /// <param name="directory">The output directory.</param>
    /// <param name="stem">The cleaned stem.</param>
    /// <returns>A stem that does not collide with an existing download.</returns>
    public static string ResolveUniqueStem(string directory, string stem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);

        string candidate = stem;
        for (int index = 1; IsTaken(directory, candidate); index++)
        {
            string suffix = "-" + index.ToString(CultureInfo.InvariantCulture);
            int keep = Math.Max(1, MaxStemLength - suffix.Length);
            candidate = Truncate(stem, keep).TrimEnd(' ', '.') + suffix;
        }

        return candidate;
    }

    private static string ReplaceIllegal(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            char character = characters[index];
            if (char.IsControl(character) || Array.IndexOf(_illegalCharacters, character) >= 0)
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }

    private static bool IsTaken(string directory, string stem)
    {
        return File.Exists(Path.Combine(directory, stem + Extension)) ||
            File.Exists(Path.Combine(directory, stem + PartExtension));
    }

    private static string CreateFallback(DateTimeOffset capturedAt)
    {
        return FallbackPrefix + capturedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
    }

    private static string Truncate(string value, int maxLength)
    {
        int length = Math.Min(value.Length, maxLength);
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }
}
