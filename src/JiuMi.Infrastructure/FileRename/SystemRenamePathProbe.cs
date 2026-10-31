using JiuMi.Application.FileRename;

namespace JiuMi.Infrastructure.FileRename;

/// <summary>
/// Checks target occupancy against the real file system.
/// </summary>
public sealed class SystemRenamePathProbe : IRenamePathProbe
{
    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <inheritdoc />
    public bool IsDirectory(string path) => Directory.Exists(path);
}
