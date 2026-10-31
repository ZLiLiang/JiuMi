using JiuMi.Application.FileRename;
using JiuMi.Infrastructure.FileRename;
using Serilog;

namespace JiuMi.Infrastructure.Tests;

public sealed class RenameExecutorTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _work;
    private readonly RenameExecutor _executor;

    public RenameExecutorTests()
    {
        _work = Path.Combine(_root, "work");
        Directory.CreateDirectory(_work);
        _executor = new RenameExecutor(new FixedTimeProvider(), new LoggerConfiguration().CreateLogger());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_GivenTwoFiles_RenamesBothAndCapturesStamps()
    {
        string a = WriteFile("a.txt", "alpha");
        string b = WriteFile("b.txt", "beta");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false),
             new RenameExecutionItem(b, Path.Combine(_work, "y.txt"), false)],
            CreateBackup: false));

        Assert.All(result.Items, item => Assert.True(item.Succeeded));
        Assert.True(File.Exists(Path.Combine(_work, "x.txt")));
        Assert.True(File.Exists(Path.Combine(_work, "y.txt")));
        Assert.False(File.Exists(a));
        Assert.All(result.Items, item => Assert.NotNull(item.Stamp));
        Assert.Null(result.BackupDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_GivenTakenTarget_LeavesTheSourceInPlace()
    {
        string a = WriteFile("a.txt", "alpha");
        WriteFile("taken.txt", "taken");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(a, Path.Combine(_work, "taken.txt"), false)],
            CreateBackup: false));

        RenameItemOutcome item = Assert.Single(result.Items);
        Assert.Equal(RenameFailure.TargetExists, item.Failure);
        Assert.True(File.Exists(a));
        Assert.Equal("taken", File.ReadAllText(Path.Combine(_work, "taken.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_GivenCaseOnlyRename_ChangesTheName()
    {
        string source = WriteFile("report.txt", "alpha");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(source, Path.Combine(_work, "REPORT.txt"), false)],
            CreateBackup: false));

        Assert.True(Assert.Single(result.Items).Succeeded);
        Assert.True(File.Exists(Path.Combine(_work, "REPORT.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_GivenReadOnlyFile_RenamesIt()
    {
        string source = WriteFile("readonly.txt", "alpha");
        File.SetAttributes(source, FileAttributes.ReadOnly);
        string target = Path.Combine(_work, "renamed.txt");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(source, target, false)],
            CreateBackup: false));

        Assert.True(Assert.Single(result.Items).Succeeded);
        Assert.True(File.Exists(target));
        File.SetAttributes(target, FileAttributes.Normal);
    }

    [Fact]
    public async Task ExecuteAsync_GivenMissingSource_ReportsSourceMissing()
    {
        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(Path.Combine(_work, "missing.txt"), Path.Combine(_work, "x.txt"), false)],
            CreateBackup: false));

        Assert.Equal(RenameFailure.SourceMissing, Assert.Single(result.Items).Failure);
    }

    [Fact]
    public async Task ExecuteAsync_GivenBackup_CopiesFilesAndFoldersBesideTheSelectedDirectory()
    {
        string file = WriteFile("a.txt", "alpha");
        string folder = Path.Combine(_work, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.txt"), "child");
        string renamedFolder = Path.Combine(_work, "renamed-folder");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(file, Path.Combine(_work, "b.txt"), false),
             new RenameExecutionItem(folder, renamedFolder, true)],
            CreateBackup: true));

        Assert.All(result.Items, item => Assert.True(item.Succeeded));
        Assert.NotNull(result.BackupDirectory);
        Assert.Equal(_root, Path.GetDirectoryName(result.BackupDirectory));
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory!, "001-a.txt")));
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory!, "002-folder", "child.txt")));
        Assert.True(Directory.Exists(renamedFolder));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ExecuteAsync_GivenExistingBackupFolder_AddsANumericSuffix()
    {
        string file = WriteFile("a.txt", "alpha");
        Directory.CreateDirectory(Path.Combine(_root, "JiuMi-backup-20261031-153000"));

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(file, Path.Combine(_work, "b.txt"), false)],
            CreateBackup: true));

        Assert.Equal(
            "JiuMi-backup-20261031-153000-2",
            Path.GetFileName(result.BackupDirectory));
    }

    [Fact]
    public async Task UndoAsync_GivenRenamedFiles_RestoresThemInReverseOrder()
    {
        string a = WriteFile("a.txt", "alpha");
        string b = WriteFile("b.txt", "beta");
        RenameOperationResult executed = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false),
             new RenameExecutionItem(b, Path.Combine(_work, "y.txt"), false)],
            CreateBackup: false));

        RenameOperationResult undone = await _executor.UndoAsync(
            [.. executed.Items.Select(item => new RenameUndoItem(
                item.SourcePath,
                item.TargetPath,
                item.IsDirectory,
                item.Stamp))]);

        Assert.All(undone.Items, item => Assert.True(item.Succeeded));
        Assert.Equal("alpha", File.ReadAllText(a));
        Assert.Equal("beta", File.ReadAllText(b));
        Assert.False(File.Exists(Path.Combine(_work, "x.txt")));
    }

    [Fact]
    public async Task UndoAsync_GivenRenamedFolder_RestoresIt()
    {
        string folder = Path.Combine(_work, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.txt"), "child");
        RenameOperationResult executed = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(folder, Path.Combine(_work, "renamed-folder"), true)],
            CreateBackup: false));
        RenameItemOutcome item = Assert.Single(executed.Items);

        RenameOperationResult undone = await _executor.UndoAsync(
            [new RenameUndoItem(item.SourcePath, item.TargetPath, item.IsDirectory, item.Stamp)]);

        Assert.True(Assert.Single(undone.Items).Succeeded);
        Assert.True(Directory.Exists(folder));
        Assert.False(Directory.Exists(Path.Combine(_work, "renamed-folder")));
    }

    [Fact]
    public async Task UndoAsync_GivenExternallyChangedFolder_KeepsTheRenamedFolder()
    {
        string folder = Path.Combine(_work, "folder");
        Directory.CreateDirectory(folder);
        RenameOperationResult executed = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(folder, Path.Combine(_work, "renamed-folder"), true)],
            CreateBackup: false));
        string renamed = Path.Combine(_work, "renamed-folder");
        File.WriteAllText(Path.Combine(renamed, "added.txt"), "added");
        Directory.SetLastWriteTimeUtc(renamed, DateTime.UtcNow.AddMinutes(1));
        RenameItemOutcome item = Assert.Single(executed.Items);

        RenameOperationResult undone = await _executor.UndoAsync(
            [new RenameUndoItem(item.SourcePath, item.TargetPath, item.IsDirectory, item.Stamp)]);

        Assert.Equal(RenameFailure.Modified, Assert.Single(undone.Items).Failure);
        Assert.True(Directory.Exists(renamed));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task UndoAsync_GivenExternallyChangedFile_KeepsTheRenamedFile()
    {
        string a = WriteFile("a.txt", "alpha");
        RenameOperationResult executed = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false)],
            CreateBackup: false));
        string renamed = Path.Combine(_work, "x.txt");
        File.WriteAllText(renamed, "changed by another program");
        File.SetLastWriteTimeUtc(renamed, DateTime.UtcNow.AddMinutes(1));
        RenameItemOutcome item = Assert.Single(executed.Items);

        RenameOperationResult undone = await _executor.UndoAsync(
            [new RenameUndoItem(item.SourcePath, item.TargetPath, item.IsDirectory, item.Stamp)]);

        Assert.Equal(RenameFailure.Modified, Assert.Single(undone.Items).Failure);
        Assert.True(File.Exists(renamed));
        Assert.False(File.Exists(a));
    }

    [Fact]
    public async Task UndoAsync_GivenMissingRenamedFile_ReportsSourceMissing()
    {
        string a = WriteFile("a.txt", "alpha");
        RenameOperationResult executed = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false)],
            CreateBackup: false));
        File.Delete(Path.Combine(_work, "x.txt"));
        RenameItemOutcome item = Assert.Single(executed.Items);

        RenameOperationResult undone = await _executor.UndoAsync(
            [new RenameUndoItem(item.SourcePath, item.TargetPath, item.IsDirectory, item.Stamp)]);

        Assert.Equal(RenameFailure.SourceMissing, Assert.Single(undone.Items).Failure);
    }

    private string WriteFile(string name, string content)
    {
        string path = Path.Combine(_work, name);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 31, 15, 30, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
