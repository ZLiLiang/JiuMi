using JiuMi.Application.FileRename;

namespace JiuMi.ViewModels;

/// <summary>
/// Maps rename domain values to the localization keys the page shows.
/// </summary>
internal static class FileRenameText
{
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
