using System.Security;
using JiuMi.Application.FileRename;

namespace JiuMi.Infrastructure.FileRename;

/// <summary>
/// Reads every source with <see cref="System.IO"/>. Sources are visited in add order, directly
/// added files win over the same path found by a folder, and links are never followed.
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
        List<SourceTally> tallies =
        [
            .. request.Sources
                .OrderBy(source => source.Order)
                .Select(source => new SourceTally(source)),
        ];

        var candidates = new List<RenameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> extensions = RenameExtensions.Parse(request.Extensions);

        // Direct files go first even when their source was added later, so an explicit selection
        // wins over the same path found by a folder source.
        if (request.Target == RenameTarget.Files)
        {
            foreach (SourceTally tally in tallies)
            {
                if (tally.Kind == RenameSourceKind.Files)
                {
                    ScanDirectFiles(tally, extensions, seen, candidates, cancellationToken);
                }
            }
        }

        foreach (SourceTally tally in tallies)
        {
            if (tally.Kind != RenameSourceKind.Folder)
            {
                continue;
            }

            string? root = TryGetFullPath(tally.FolderPath);
            if (root is null || !Directory.Exists(root))
            {
                continue;
            }

            if (request.Target == RenameTarget.Files)
            {
                VisitFiles(
                    tally,
                    root,
                    root,
                    request.IncludeSubdirectories,
                    extensions,
                    seen,
                    candidates,
                    cancellationToken);
            }
            else
            {
                VisitFolders(
                    tally,
                    root,
                    root,
                    request.IncludeSubdirectories,
                    seen,
                    candidates,
                    cancellationToken);
            }
        }

        return new RenameScanResult(
            [.. candidates.OrderBy(candidate => candidate.SourceOrder)],
            [.. tallies.Select(tally => tally.ToSummary())]);
    }

    private void ScanDirectFiles(
        SourceTally tally,
        IReadOnlyList<string> extensions,
        HashSet<string> seen,
        List<RenameCandidate> candidates,
        CancellationToken cancellationToken)
    {
        foreach (string addedPath in tally.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? path = TryGetFullPath(addedPath);
            if (path is null)
            {
                continue;
            }

            FileAttributes? attributes = TryGetAttributes(path);
            if (attributes is { } value && value.HasFlag(FileAttributes.Directory))
            {
                continue;
            }

            RenameSkipReason? reason = attributes is { } present
                ? Classify(present, isDirectory: false)
                : null;
            if (reason is null)
            {
                tally.RecordSeen();
            }

            if (reason is null && !RenameExtensions.Allows(Path.GetFileName(path), extensions))
            {
                tally.RecordFiltered(path);
                continue;
            }

            Accept(tally, path, Path.GetFileName(path), isDirectory: false, reason, seen, candidates);
        }
    }

    private void VisitFiles(
        SourceTally tally,
        string root,
        string directory,
        bool recurse,
        IReadOnlyList<string> extensions,
        HashSet<string> seen,
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
                    VisitFiles(tally, root, entry, recurse, extensions, seen, candidates, cancellationToken);
                }

                continue;
            }

            if (reason is null)
            {
                tally.RecordSeen();
            }

            if (reason is null && !RenameExtensions.Allows(Path.GetFileName(entry), extensions))
            {
                tally.RecordFiltered(entry);
                continue;
            }

            Accept(tally, entry, RelativePath(root, entry), isDirectory: false, reason, seen, candidates);
        }
    }

    private void VisitFolders(
        SourceTally tally,
        string root,
        string directory,
        bool recurse,
        HashSet<string> seen,
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
            if (reason is null)
            {
                tally.RecordSeen();
            }

            if (!Accept(tally, entry, RelativePath(root, entry), isDirectory: true, reason, seen, candidates))
            {
                continue;
            }

            if (recurse && reason is null)
            {
                VisitFolders(tally, root, entry, recurse, seen, candidates, cancellationToken);
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

    private string? TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is
            ArgumentException or NotSupportedException or SecurityException or IOException)
        {
            _logger.Warning(exception, "The rename source path {Path} is not valid", path);
            return null;
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

    private static bool Accept(
        SourceTally tally,
        string path,
        string relativePath,
        bool isDirectory,
        RenameSkipReason? reason,
        HashSet<string> seen,
        List<RenameCandidate> candidates)
    {
        if (!seen.Add(path))
        {
            return false;
        }

        candidates.Add(new RenameCandidate(path, relativePath, isDirectory, tally.Order, reason));
        if (reason is null)
        {
            tally.RecordContribution();
        }

        return true;
    }

    private sealed class SourceTally
    {
        private readonly RenameSource _source;
        private readonly List<string> _filteredPaths = [];
        private int _totalCount;
        private int _contributionCount;

        public SourceTally(RenameSource source)
        {
            _source = source;
        }

        public int Order => _source.Order;

        public RenameSourceKind Kind => _source.Kind;

        public IReadOnlyList<string> Paths => _source.Paths;

        public string FolderPath => _source.Paths[0];

        public void RecordSeen() => _totalCount++;

        public void RecordContribution() => _contributionCount++;

        public void RecordFiltered(string path) => _filteredPaths.Add(path);

        public RenameSourceScanSummary ToSummary() =>
            new(_source, _totalCount, _contributionCount, [.. _filteredPaths]);
    }
}
