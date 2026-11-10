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
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false, "1-work"),
             new RenameExecutionItem(b, Path.Combine(_work, "y.txt"), false, "1-work")],
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
            [new RenameExecutionItem(a, Path.Combine(_work, "taken.txt"), false, "1-work")],
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
            [new RenameExecutionItem(source, Path.Combine(_work, "REPORT.txt"), false, "1-work")],
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

        try
        {
            RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
                _work,
                [new RenameExecutionItem(source, target, false, "1-work")],
                CreateBackup: false));

            Assert.True(Assert.Single(result.Items).Succeeded);
            Assert.True(File.Exists(target));
        }
        finally
        {
            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
            }

            if (File.Exists(source))
            {
                File.SetAttributes(source, FileAttributes.Normal);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_GivenProgress_ReportsEachOutcome()
    {
        string a = WriteFile("a.txt", "alpha");
        string b = WriteFile("b.txt", "beta");
        var reported = new List<RenameItemOutcome>();

        await _executor.ExecuteAsync(
            new RenameExecutionRequest(
                _work,
                [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false, "1-work"),
                 new RenameExecutionItem(b, Path.Combine(_work, "y.txt"), false, "1-work")],
                CreateBackup: false),
            new InlineProgress(reported));

        Assert.Equal(2, reported.Count);
        Assert.All(reported, item => Assert.True(item.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_GivenMissingSource_ReportsSourceMissing()
    {
        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(Path.Combine(_work, "missing.txt"), Path.Combine(_work, "x.txt"), false, "1-work")],
            CreateBackup: false));

        Assert.Equal(RenameFailure.SourceMissing, Assert.Single(result.Items).Failure);
    }

    [Fact]
    public async Task ExecuteAsync_GivenBackup_CopiesFilesAndFoldersIntoTheirSourceGroup()
    {
        string file = WriteFile("a.txt", "alpha");
        string folder = Path.Combine(_work, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.txt"), "child");
        string renamedFolder = Path.Combine(_work, "renamed-folder");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _root,
            [new RenameExecutionItem(file, Path.Combine(_work, "b.txt"), false, "1-work"),
             new RenameExecutionItem(folder, renamedFolder, true, "1-work")],
            CreateBackup: true));

        Assert.All(result.Items, item => Assert.True(item.Succeeded));
        Assert.NotNull(result.BackupDirectory);
        Assert.Equal(_root, Path.GetDirectoryName(result.BackupDirectory));
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory!, "1-work", "001-a.txt")));
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory!, "1-work", "002-folder", "child.txt")));
        Assert.True(Directory.Exists(renamedFolder));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ExecuteAsync_GivenTwoSources_GroupsTheirBackupsUnderOneRoot()
    {
        string a = WriteFile("a.txt", "alpha");
        string other = Path.Combine(_root, "other");
        Directory.CreateDirectory(other);
        string b = Path.Combine(other, "b.txt");
        File.WriteAllText(b, "beta");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _root,
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false, "1-work"),
             new RenameExecutionItem(b, Path.Combine(other, "y.txt"), false, "2-other")],
            CreateBackup: true));

        Assert.All(result.Items, item => Assert.True(item.Succeeded));
        string backup = result.BackupDirectory!;
        Assert.Equal(_root, Path.GetDirectoryName(backup));
        Assert.True(File.Exists(Path.Combine(backup, "1-work", "001-a.txt")));
        Assert.True(File.Exists(Path.Combine(backup, "2-other", "002-b.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_GivenASourceBeyondTheBackupRoot_CopiesItIntoItsGroup()
    {
        string outside = Path.Combine(
            Path.GetTempPath(),
            "JiuMi.Tests.Source." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        string file = Path.Combine(outside, "outside.txt");
        File.WriteAllText(file, "outside");

        try
        {
            RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
                _root,
                [new RenameExecutionItem(file, Path.Combine(outside, "renamed.txt"), false, "1-outside")],
                CreateBackup: true));

            Assert.True(Assert.Single(result.Items).Succeeded);
            Assert.True(File.Exists(Path.Combine(result.BackupDirectory!, "1-outside", "001-outside.txt")));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_GivenOneBackupFailure_LeavesThatItemInPlaceAndRenamesTheOthers()
    {
        string locked = WriteFile("locked.txt", "locked");
        string free = WriteFile("free.txt", "free");
        RenameOperationResult result;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await _executor.ExecuteAsync(new RenameExecutionRequest(
                _root,
                [new RenameExecutionItem(locked, Path.Combine(_work, "locked-renamed.txt"), false, "1-work"),
                 new RenameExecutionItem(free, Path.Combine(_work, "free-renamed.txt"), false, "1-work")],
                CreateBackup: true));
        }

        Assert.Equal(RenameFailure.BackupFailed, result.Items[0].Failure);
        Assert.True(result.Items[1].Succeeded);
        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(Path.Combine(_work, "locked-renamed.txt")));
        Assert.True(File.Exists(Path.Combine(_work, "free-renamed.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_GivenExistingBackupFolder_AddsANumericSuffix()
    {
        string file = WriteFile("a.txt", "alpha");
        Directory.CreateDirectory(Path.Combine(_work, "JiuMi-backup-20261031-153000"));

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(file, Path.Combine(_work, "b.txt"), false, "1-work")],
            CreateBackup: true));

        Assert.Equal(
            "JiuMi-backup-20261031-153000-2",
            Path.GetFileName(result.BackupDirectory));
    }

    [Fact]
    public async Task ExecuteAsync_GivenAnEmptyBackupRoot_FailsEveryItemWithoutRenamingIt()
    {
        string file = WriteFile("a.txt", "alpha");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            string.Empty,
            [new RenameExecutionItem(file, Path.Combine(_work, "b.txt"), false, "1-work")],
            CreateBackup: true));

        Assert.Equal(RenameFailure.BackupFailed, Assert.Single(result.Items).Failure);
        Assert.Null(result.BackupDirectory);
        Assert.True(File.Exists(file));
        Assert.False(File.Exists(Path.Combine(_work, "b.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_GivenAnEmptyBackupGroup_WritesDirectlyUnderTheBackupDirectory()
    {
        string file = WriteFile("a.txt", "alpha");

        RenameOperationResult result = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _root,
            [new RenameExecutionItem(file, Path.Combine(_work, "b.txt"), false, string.Empty)],
            CreateBackup: true));

        Assert.True(Assert.Single(result.Items).Succeeded);
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory!, "001-a.txt")));
    }

    [Fact]
    public async Task UndoAsync_GivenRenamedFiles_RestoresThemInReverseOrder()
    {
        string a = WriteFile("a.txt", "alpha");
        string b = WriteFile("b.txt", "beta");
        RenameOperationResult executed = await _executor.ExecuteAsync(new RenameExecutionRequest(
            _work,
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false, "1-work"),
             new RenameExecutionItem(b, Path.Combine(_work, "y.txt"), false, "1-work")],
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
            [new RenameExecutionItem(folder, Path.Combine(_work, "renamed-folder"), true, "1-work")],
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
            [new RenameExecutionItem(folder, Path.Combine(_work, "renamed-folder"), true, "1-work")],
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
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false, "1-work")],
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
            [new RenameExecutionItem(a, Path.Combine(_work, "x.txt"), false, "1-work")],
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

    private sealed class InlineProgress : IProgress<RenameItemOutcome>
    {
        private readonly List<RenameItemOutcome> _reported;

        public InlineProgress(List<RenameItemOutcome> reported)
        {
            _reported = reported;
        }

        public void Report(RenameItemOutcome value) => _reported.Add(value);
    }
}
