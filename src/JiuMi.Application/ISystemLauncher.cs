namespace JiuMi.Application;

/// <summary>
/// Opens folders and links with the operating system's default handler.
/// </summary>
public interface ISystemLauncher
{
    /// <summary>
    /// Opens <paramref name="path"/> in the system file manager.
    /// </summary>
    /// <param name="path">The file or folder to show.</param>
    void OpenFolder(string path);

    /// <summary>
    /// Opens <paramref name="uri"/> with the system's default handler.
    /// </summary>
    /// <param name="uri">The absolute URI to open.</param>
    void OpenUri(Uri uri);

    /// <summary>
    /// Opens <paramref name="path"/> with the system's default application for that file type.
    /// </summary>
    /// <param name="path">The local file path to open.</param>
    void OpenFile(string path);

    /// <summary>
    /// Opens the system file manager and highlights <paramref name="filePath"/> when supported
    /// by the platform, falling back to opening its parent folder otherwise.
    /// </summary>
    /// <param name="filePath">The local file path to reveal.</param>
    void RevealInFolder(string filePath);
}
