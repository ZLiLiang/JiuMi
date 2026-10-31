using JiuMi.Application.FileRename;

namespace JiuMi.Infrastructure.FileRename;

/// <summary>
/// Reads one selected directory with <see cref="System.IO"/>. It never follows links and never
/// enters a hidden or system folder.
/// </summary>
public sealed class RenameScanner : IRenameScanner
{
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameScanner"/> class.
    /// </summary>
    /// <param name="logger">Receives directory enumeration failures.</param>
    public RenameScanner(Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RenameScanResult> ScanAsync(
        RenameScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Directory enumeration is synchronous; run the walk off the caller's thread.
        return Task.Run(() => Scan(request, cancellationToken), cancellationToken);
    }

    private RenameScanResult Scan(RenameScanRequest request, CancellationToken cancellationToken)
    {
        string root;
        try
        {
            root = Path.GetFullPath(request.DirectoryPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            _logger.Warning(exception, "The rename directory is not a valid path");
            return new RenameScanResult([]);
        }

        if (!Directory.Exists(root))
        {
            return new RenameScanResult([]);
        }

        var candidates = new List<RenameCandidate>();
        if (request.Target == RenameTarget.Files)
        {
            IReadOnlyList<string> extensions = RenameExtensions.Parse(request.Extensions);
            VisitFiles(root, root, request.IncludeSubdirectories, extensions, candidates, cancellationToken);
        }
        else
        {
            VisitFolders(root, root, request.IncludeSubdirectories, candidates, cancellationToken);
        }

        return new RenameScanResult(candidates);
    }

    private void VisitFiles(
        string root,
        string directory,
        bool recurse,
        IReadOnlyList<string> extensions,
        List<RenameCandidate> candidates,
        CancellationToken cancellationToken)
    {
        foreach (string entry in Enumerate(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetAttributes(entry) is not { } attributes)
            {
                continue;
            }

            bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
            RenameSkipReason? reason = Classify(attributes, isDirectory);
            if (isDirectory)
            {
                if (recurse && reason is null)
                {
                    VisitFiles(root, entry, recurse, extensions, candidates, cancellationToken);
                }

                continue;
            }

            if (reason is null && !RenameExtensions.Allows(Path.GetFileName(entry), extensions))
            {
                continue;
            }

            candidates.Add(new RenameCandidate(entry, RelativePath(root, entry), IsDirectory: false, reason));
        }
    }

    private void VisitFolders(
        string root,
        string directory,
        bool recurse,
        List<RenameCandidate> candidates,
        CancellationToken cancellationToken)
    {
        foreach (string entry in Enumerate(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetAttributes(entry) is not { } attributes ||
                !attributes.HasFlag(FileAttributes.Directory))
            {
                continue;
            }

            RenameSkipReason? reason = Classify(attributes, isDirectory: true);
            candidates.Add(new RenameCandidate(entry, RelativePath(root, entry), IsDirectory: true, reason));
            if (recurse && reason is null)
            {
                VisitFolders(root, entry, recurse, candidates, cancellationToken);
            }
        }
    }

    private string[] Enumerate(string directory)
    {
        try
        {
            return Directory.GetFileSystemEntries(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(exception, "Reading rename candidates from {Directory} failed", directory);
            return [];
        }
    }

    private FileAttributes? TryGetAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(exception, "Reading the attributes of {Path} failed", path);
            return null;
        }
    }

    private static RenameSkipReason? Classify(FileAttributes attributes, bool isDirectory)
    {
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return isDirectory ? RenameSkipReason.Junction : RenameSkipReason.SymbolicLink;
        }

        if (attributes.HasFlag(FileAttributes.System))
        {
            return RenameSkipReason.System;
        }

        return attributes.HasFlag(FileAttributes.Hidden) ? RenameSkipReason.Hidden : null;
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
