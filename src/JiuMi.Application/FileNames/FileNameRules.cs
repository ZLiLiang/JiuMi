namespace JiuMi.Application.FileNames;

/// <summary>
/// Checks names against the Windows file system rules shared by the tools that write file names.
/// The rules report problems; they never repair a name.
/// </summary>
public static class FileNameRules
{
    /// <summary>The longest file name Windows accepts.</summary>
    public const int MaxNameLength = 255;

    /// <summary>The longest full path Windows accepts without long-path opt-in.</summary>
    public const int MaxPathLength = 260;

    private static readonly char[] _illegalCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// Finds the first problem with a file name.
    /// </summary>
    /// <param name="name">The file name, without a directory.</param>
    /// <param name="fullPath">The full path the name will be used in, when known.</param>
    /// <returns>The first problem, or <see cref="FileNameIssue.None"/> when the name is usable.</returns>
    public static FileNameIssue FindIssue(string name, string? fullPath = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 0)
        {
            return FileNameIssue.Empty;
        }

        foreach (char character in name)
        {
            if (character < ' ' || Array.IndexOf(_illegalCharacters, character) >= 0)
            {
                return FileNameIssue.IllegalCharacter;
            }
        }

        if (name[^1] is ' ' or '.')
        {
            return FileNameIssue.TrailingSpaceOrDot;
        }

        if (IsReservedName(name))
        {
            return FileNameIssue.ReservedName;
        }

        if (name.Length > MaxNameLength || fullPath is { Length: >= MaxPathLength })
        {
            return FileNameIssue.TooLong;
        }

        return FileNameIssue.None;
    }

    /// <summary>
    /// Checks whether a name is a Windows device name, with or without an extension.
    /// </summary>
    /// <param name="name">The file name, without a directory.</param>
    /// <returns><see langword="true"/> when Windows reserves the name.</returns>
    public static bool IsReservedName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        int dot = name.IndexOf('.');
        ReadOnlySpan<char> stem = (dot < 0 ? name.AsSpan() : name.AsSpan(0, dot)).TrimEnd(' ');

        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (stem.Length != 4)
        {
            return false;
        }

        ReadOnlySpan<char> prefix = stem[..3];
        bool isPort = prefix.Equals("COM", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("LPT", StringComparison.OrdinalIgnoreCase);
        return isPort && stem[3] is >= '1' and <= '9';
    }

    /// <summary>
    /// Splits a name into its stem and extension; a leading dot does not start an extension.
    /// </summary>
    /// <param name="name">The file name, without a directory.</param>
    /// <returns>The stem and the extension, including its dot.</returns>
    public static (string Stem, string Extension) SplitExtension(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        int dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1
            ? (name, string.Empty)
            : (name[..dot], name[dot..]);
    }
}
