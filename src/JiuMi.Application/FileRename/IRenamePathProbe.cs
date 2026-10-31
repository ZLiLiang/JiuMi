namespace JiuMi.Application.FileRename;

/// <summary>
/// Answers whether a path is taken. The planner stays pure by asking this seam instead of reading
/// the file system itself.
/// </summary>
public interface IRenamePathProbe
{
    /// <summary>
    /// Checks whether a file or folder exists at the path.
    /// </summary>
    /// <param name="path">The absolute path.</param>
    /// <returns><see langword="true"/> when the path is taken.</returns>
    bool Exists(string path);

    /// <summary>
    /// Checks whether the path is an existing folder.
    /// </summary>
    /// <param name="path">The absolute path.</param>
    /// <returns><see langword="true"/> when the path is an existing folder.</returns>
    bool IsDirectory(string path);
}
