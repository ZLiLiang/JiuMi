using System.IO;
using JiuMi.Application.FileRename;

namespace JiuMi.ViewModels;

/// <summary>
/// Maps rename domain values to the localization keys the page shows.
/// </summary>
internal static class FileRenameText
{
    /// <summary>
    /// Returns the last segment of a directory path, falling back to the drive label.
    /// </summary>
    /// <param name="directory">The absolute directory path, or <see langword="null"/>.</param>
    /// <returns>The segment shown after a preview row, or an empty string.</returns>
    public static string DirectoryLabel(string? directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        string trimmed = Path.TrimEndingDirectorySeparator(directory);
        string name = Path.GetFileName(trimmed);
        if (name.Length > 0)
        {
            return name;
        }

        string? root = Path.GetPathRoot(trimmed);
        return string.IsNullOrEmpty(root)
            ? string.Empty
            : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':');
    }

    /// <summary>
    /// Gets the reason key of a precheck error.
    /// </summary>
    /// <param name="error">The precheck error.</param>
    /// <returns>The localization key.</returns>
    public static string ReasonKey(RenamePrecheckError error) => error switch
    {
        RenamePrecheckError.EmptyName => "Tool.FileRename.Reason.EmptyName",
        RenamePrecheckError.IllegalCharacter => "Tool.FileRename.Reason.IllegalChar",
        RenamePrecheckError.TrailingSpaceOrDot => "Tool.FileRename.Reason.Trailing",
        RenamePrecheckError.ReservedName => "Tool.FileRename.Reason.Reserved",
        RenamePrecheckError.TooLong => "Tool.FileRename.Reason.TooLong",
        RenamePrecheckError.Duplicate => "Tool.FileRename.Reason.Duplicate",
        _ => "Tool.FileRename.Reason.Occupied",
    };
}
