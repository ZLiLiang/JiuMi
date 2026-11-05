namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Remembers the last directory the user chose on the image conversion page for the rest of this
/// process session. The value is never written to the settings file.
/// </summary>
public sealed class ImageConvertPickerDirectories
{
    private string? _lastDirectory;

    /// <summary>
    /// Gets the remembered directory, or <see langword="null"/> when the user has not chosen one in
    /// this session.
    /// </summary>
    public string? SourceDirectory => _lastDirectory;

    /// <summary>
    /// Gets the directory the output picker opens at: the custom output directory when one is set,
    /// otherwise the remembered directory.
    /// </summary>
    /// <param name="customOutputDirectory">The output directory kept in the settings.</param>
    /// <returns>The start directory, or <see langword="null"/> for the system default.</returns>
    public string? GetOutputDirectory(string? customOutputDirectory) =>
        string.IsNullOrWhiteSpace(customOutputDirectory)
            ? _lastDirectory
            : customOutputDirectory.Trim();

    /// <summary>
    /// Remembers a directory the user chose. Blank values are ignored, so the memory is never
    /// cleared during the session.
    /// </summary>
    /// <param name="directory">The chosen directory.</param>
    /// <returns>Whether the remembered value changed.</returns>
    public bool Remember(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        string normalized = directory.Trim();
        if (string.Equals(normalized, _lastDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _lastDirectory = normalized;
        return true;
    }
}
