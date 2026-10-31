using System.Globalization;

namespace JiuMi.Application.FileRename;

/// <summary>
/// Parses and matches the extension filter typed on the page. The filter never changes a name;
/// it only decides which files enter the scan.
/// </summary>
public static class RenameExtensions
{
    private static readonly char[] _separators = [' ', ',', ';', '\t'];

    /// <summary>
    /// Splits the typed filter into normalized extensions with a leading dot and lower-case letters.
    /// </summary>
    /// <param name="text">The text typed by the user.</param>
    /// <returns>The distinct extensions; empty when no filter is set.</returns>
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var extensions = new List<string>();
        foreach (string part in text.Split(_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            string extension = part.Trim().TrimStart('*');
            if (extension.Length == 0)
            {
                continue;
            }

            if (extension[0] != '.')
            {
                extension = "." + extension;
            }

            extension = extension.ToLower(CultureInfo.InvariantCulture);
            if (!extensions.Contains(extension, StringComparer.Ordinal))
            {
                extensions.Add(extension);
            }
        }

        return extensions;
    }

    /// <summary>
    /// Checks whether a file name passes the filter.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="extensions">The normalized extensions; empty means no filter.</param>
    /// <returns><see langword="true"/> when the file enters the scan.</returns>
    public static bool Allows(string name, IReadOnlyList<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(extensions);

        if (extensions.Count == 0)
        {
            return true;
        }

        foreach (string extension in extensions)
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
