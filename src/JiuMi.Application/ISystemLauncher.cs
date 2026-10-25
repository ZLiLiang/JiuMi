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
}
