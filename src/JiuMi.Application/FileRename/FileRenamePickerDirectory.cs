namespace JiuMi.Application.FileRename;

/// <summary>
/// Remembers the last directory the user chose on the batch rename page for the rest of this
/// process session. The value is never written to the settings file.
/// </summary>
public sealed class FileRenamePickerDirectory
{
    private string? _directory;

    /// <summary>
    /// Gets the remembered directory, or <see langword="null"/> when the user has not chosen one in
    /// this session.
    /// </summary>
    public string? Directory => _directory;

    /// <summary>
    /// Remembers a directory the user chose. Blank values are ignored, so the memory is never
    /// cleared during the session.
    /// </summary>
    /// <param name="directory">The chosen directory.</param>
    public void Remember(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        string normalized = directory.Trim();
        if (string.Equals(normalized, _directory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _directory = normalized;
    }
}
