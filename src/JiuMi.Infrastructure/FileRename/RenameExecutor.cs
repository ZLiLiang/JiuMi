using System.Globalization;
using System.Security;
using JiuMi.Application.FileRename;

namespace JiuMi.Infrastructure.FileRename;

/// <summary>
/// Renames and restores items with <see cref="System.IO"/>. It continues after a single item fails
/// and copies items to a sibling backup folder before renaming when backups are enabled.
/// </summary>
public sealed class RenameExecutor : IRenameExecutor
{
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameExecutor"/> class.
    /// </summary>
    /// <param name="timeProvider">Supplies the backup folder timestamp.</param>
    /// <param name="logger">Receives per-item failures.</param>
    public RenameExecutor(TimeProvider timeProvider, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RenameOperationResult> ExecuteAsync(
        RenameExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Items);

        // The Win32 move and copy APIs are synchronous; the whole batch runs off the caller's thread.
        return Task.Run(() => ExecuteCore(request, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<RenameOperationResult> UndoAsync(
        IReadOnlyList<RenameUndoItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Moving is synchronous; run the whole loop off the caller's thread.
        return Task.Run(() => UndoCore(items, cancellationToken), cancellationToken);
    }

    private RenameOperationResult ExecuteCore(
        RenameExecutionRequest request,
        CancellationToken cancellationToken)
    {
        string? backupDirectory = null;
        if (request.CreateBackup)
        {
            backupDirectory = CreateBackupDirectory(request.SourceRoot);
            if (backupDirectory is null)
            {
                return new RenameOperationResult(
                    [.. request.Items.Select(item => Failed(item, RenameFailure.BackupFailed))]);
            }
        }

        var outcomes = new List<RenameItemOutcome>(request.Items.Count);
        for (int index = 0; index < request.Items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenameExecutionItem item = request.Items[index];
            if (backupDirectory is not null &&
                !CopyToBackup(item, backupDirectory, index + 1, cancellationToken))
            {
                outcomes.Add(Failed(item, RenameFailure.BackupFailed));
                continue;
            }

            outcomes.Add(Move(item));
        }

        return new RenameOperationResult(outcomes, backupDirectory);
    }

    private static RenameOperationResult UndoCore(
        IReadOnlyList<RenameUndoItem> items,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<RenameItemOutcome>(items.Count);
        for (int index = items.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outcomes.Add(UndoOne(items[index]));
        }

        return new RenameOperationResult(outcomes);
    }

    private static RenameItemOutcome UndoOne(RenameUndoItem item)
    {
        bool currentExists = item.IsDirectory
            ? Directory.Exists(item.RenamedPath)
            : File.Exists(item.RenamedPath);
        if (!currentExists)
        {
            return new RenameItemOutcome(
                item.OriginalPath,
                item.RenamedPath,
                item.IsDirectory,
                RenameFailure.SourceMissing);
        }

        if (item.Stamp is { } stamp && !StampMatches(item.RenamedPath, item.IsDirectory, stamp))
        {
            return new RenameItemOutcome(
                item.OriginalPath,
                item.RenamedPath,
                item.IsDirectory,
                RenameFailure.Modified);
        }

        if (PathExists(item.OriginalPath) &&
            !string.Equals(item.OriginalPath, item.RenamedPath, StringComparison.OrdinalIgnoreCase))
        {
            return new RenameItemOutcome(
                item.OriginalPath,
                item.RenamedPath,
                item.IsDirectory,
                RenameFailure.TargetExists);
        }

        try
        {
            MovePath(item.RenamedPath, item.OriginalPath, item.IsDirectory);
            return new RenameItemOutcome(item.OriginalPath, item.RenamedPath, item.IsDirectory, Failure: null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new RenameItemOutcome(
                item.OriginalPath,
                item.RenamedPath,
                item.IsDirectory,
                MapFailure(exception, item.OriginalPath));
        }
    }

    private RenameItemOutcome Move(RenameExecutionItem item)
    {
        bool sourceExists = item.IsDirectory
            ? Directory.Exists(item.SourcePath)
            : File.Exists(item.SourcePath);
        if (!sourceExists)
        {
            return Failed(item, RenameFailure.SourceMissing);
        }

        if (PathExists(item.TargetPath) &&
            !string.Equals(item.TargetPath, item.SourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(item, RenameFailure.TargetExists);
        }

        try
        {
            MovePath(item.SourcePath, item.TargetPath, item.IsDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            _logger.Warning(exception, "Renaming {SourcePath} to {TargetPath} failed", item.SourcePath, item.TargetPath);
            return Failed(item, MapFailure(exception, item.TargetPath));
        }

        RenameFileStamp? stamp = ReadStamp(item.TargetPath, item.IsDirectory);
        return new RenameItemOutcome(item.SourcePath, item.TargetPath, item.IsDirectory, Failure: null, Stamp: stamp);
    }

    private bool CopyToBackup(
        RenameExecutionItem item,
        string backupDirectory,
        int index,
        CancellationToken cancellationToken)
    {
        string destination = Path.Combine(
            backupDirectory,
            index.ToString("D3", CultureInfo.InvariantCulture) + "-" + Path.GetFileName(item.SourcePath));

        try
        {
            if (item.IsDirectory)
            {
                CopyDirectory(item.SourcePath, destination, cancellationToken);
            }
            else
            {
                File.Copy(item.SourcePath, destination, overwrite: false);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            _logger.Warning(exception, "Copying {SourcePath} to the rename backup failed", item.SourcePath);
            return false;
        }
    }

    private static void CopyDirectory(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        foreach (string entry in Directory.GetFileSystemEntries(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            string target = Path.Combine(destination, Path.GetFileName(entry));
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                CopyDirectory(entry, target, cancellationToken);
            }
            else
            {
                File.Copy(entry, target, overwrite: false);
            }
        }
    }

    private string? CreateBackupDirectory(string sourceRoot)
    {
        try
        {
            string root = Path.GetFullPath(sourceRoot);
            string? parent = Directory.GetParent(root)?.FullName;
            if (parent is null)
            {
                _logger.Warning("The rename backup folder cannot be placed beside {SourceRoot}", root);
                return null;
            }

            string timestamp = _timeProvider.GetLocalNow()
                .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string basePath = Path.Combine(parent, "JiuMi-backup-" + timestamp);
            string path = basePath;
            for (int suffix = 2; Directory.Exists(path) || File.Exists(path); suffix++)
            {
                path = basePath + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            }

            Directory.CreateDirectory(path);
            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            _logger.Warning(exception, "Creating the rename backup folder failed");
            return null;
        }
    }

    private static void MovePath(string source, string target, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target, overwrite: false);
        }
    }

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    private static RenameFileStamp? ReadStamp(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            var directory = new DirectoryInfo(path);
            return directory.Exists
                ? new RenameFileStamp(Length: 0, directory.LastWriteTimeUtc)
                : null;
        }

        var file = new FileInfo(path);
        return file.Exists ? new RenameFileStamp(file.Length, file.LastWriteTimeUtc) : null;
    }

    private static bool StampMatches(string path, bool isDirectory, RenameFileStamp stamp)
    {
        if (isDirectory)
        {
            var directory = new DirectoryInfo(path);
            return directory.Exists && directory.LastWriteTimeUtc == stamp.LastWriteTimeUtc;
        }

        var file = new FileInfo(path);
        return file.Exists &&
            file.Length == stamp.Length &&
            file.LastWriteTimeUtc == stamp.LastWriteTimeUtc;
    }

    private static RenameItemOutcome Failed(RenameExecutionItem item, RenameFailure failure) =>
        new(item.SourcePath, item.TargetPath, item.IsDirectory, failure);

    private static RenameFailure MapFailure(Exception exception, string targetPath) =>
        exception switch
        {
            UnauthorizedAccessException or SecurityException => RenameFailure.AccessDenied,
            IOException when PathExists(targetPath) => RenameFailure.TargetExists,
            _ => RenameFailure.Unknown,
        };
}
