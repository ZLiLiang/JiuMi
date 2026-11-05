using System.Globalization;

namespace JiuMi.Application.FileRename;

/// <summary>
/// One add action: a folder to scan or the files selected together. Sources keep their add order
/// and are only referenced; a folder container itself is never renamed.
/// </summary>
public sealed record RenameSource
{
    private readonly int _order;
    private readonly RenameSourceKind _kind;
    private readonly IReadOnlyList<string> _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameSource"/> class.
    /// </summary>
    /// <param name="order">The one-based add order used for grouping and for the backup folder.</param>
    /// <param name="kind">Whether the source is a folder to scan or files added directly.</param>
    /// <param name="paths">One folder path, or the absolute paths of the files added together.</param>
    public RenameSource(int order, RenameSourceKind kind, IReadOnlyList<string> paths)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            throw new ArgumentException("A rename source needs at least one path.", nameof(paths));
        }

        if (kind == RenameSourceKind.Folder && paths.Count != 1)
        {
            throw new ArgumentException("A folder source holds exactly one directory.", nameof(paths));
        }

        _order = order;
        _kind = kind;
        _paths = Array.AsReadOnly([.. paths]);
    }

    /// <summary>Gets the one-based add order.</summary>
    public int Order => _order;

    /// <summary>Gets whether the source is a folder to scan or files added directly.</summary>
    public RenameSourceKind Kind => _kind;

    /// <summary>Gets the source paths, in the order they were added.</summary>
    public IReadOnlyList<string> Paths => _paths;

    /// <summary>
    /// Gets the folder the source represents: the scanned directory, or the directory of the first
    /// directly added file.
    /// </summary>
    public string? Directory => Kind == RenameSourceKind.Folder
        ? Paths[0]
        : Path.GetDirectoryName(Paths[0]);

    /// <summary>
    /// Gets the directory that receives the backup folder when this source is the first of a
    /// batch, or <see langword="null"/> when the source sits at a drive root.
    /// </summary>
    public string? BackupRoot => Directory is { } directory
        ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory))
        : null;

    /// <summary>Gets the backup subfolder name, such as <c>1-photos</c>.</summary>
    public string BackupGroup =>
        Order.ToString(CultureInfo.InvariantCulture) + "-" + Name;

    /// <inheritdoc />
    public bool Equals(RenameSource? other) =>
        other is not null &&
        Order == other.Order &&
        Kind == other.Kind &&
        Paths.SequenceEqual(other.Paths, StringComparer.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Order);
        hash.Add(Kind);
        foreach (string path in Paths)
        {
            hash.Add(path, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private string Name
    {
        get
        {
            string? directory = Directory;
            if (string.IsNullOrEmpty(directory))
            {
                return "root";
            }

            string trimmed = Path.TrimEndingDirectorySeparator(directory);
            string name = Path.GetFileName(trimmed);
            if (name.Length > 0)
            {
                return name;
            }

            string? root = Path.GetPathRoot(trimmed);
            return string.IsNullOrEmpty(root)
                ? "root"
                : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':');
        }
    }
}
