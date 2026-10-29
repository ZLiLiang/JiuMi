namespace JiuMi.Application.ImageConversion;

/// <summary>
/// Decides the output directory and file name of one source file. The planner never overwrites an
/// existing file or a path another file in the same batch is already using.
/// </summary>
public sealed class OutputPathPlanner
{
    private readonly Func<string, bool> _fileExists;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutputPathPlanner"/> class.
    /// </summary>
    /// <param name="fileExists">Tells whether a path is already taken on disk.</param>
    public OutputPathPlanner(Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        _fileExists = fileExists;
    }

    /// <summary>
    /// Plans the output path of a source file.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="outputDirectory">The chosen directory, or <see langword="null"/> for the source directory.</param>
    /// <param name="targetFormat">The format that decides the extension.</param>
    /// <param name="reservedPaths">Paths already planned in the same batch, or <see langword="null"/>.</param>
    /// <returns>An unused path in the output directory.</returns>
    /// <exception cref="ArgumentException">The source has no usable name or directory.</exception>
    public string Plan(
        string sourcePath,
        string? outputDirectory,
        ImageFormat targetFormat,
        ISet<string>? reservedPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!ImageFormatCatalog.IsOutputFormat(targetFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(targetFormat), targetFormat, "Not an output format.");
        }

        string? directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(sourcePath)
            : outputDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("The source file has no directory to write to.", nameof(sourcePath));
        }

        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The source file name cannot be used as an output name.", nameof(sourcePath));
        }

        string extension = ImageFormatCatalog.GetExtension(targetFormat);
        string candidate = Path.Combine(directory, fileName + extension);
        if (!IsTaken(candidate, reservedPaths))
        {
            return candidate;
        }

        for (int suffix = 1; ; suffix++)
        {
            candidate = Path.Combine(directory, $"{fileName}-{suffix}{extension}");
            if (!IsTaken(candidate, reservedPaths))
            {
                return candidate;
            }
        }
    }

    private bool IsTaken(string path, ISet<string>? reservedPaths)
    {
        if (_fileExists(path))
        {
            return true;
        }

        return reservedPaths is not null && reservedPaths.Any(
            reserved => string.Equals(reserved, path, StringComparison.OrdinalIgnoreCase));
    }
}
