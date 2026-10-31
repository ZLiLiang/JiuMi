using JiuMi.Application.FileRename;
using JiuMi.Infrastructure.FileRename;
using Serilog;

namespace JiuMi.Infrastructure.Tests;

public sealed class RenameScannerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _work;
    private readonly RenameScanner _scanner;

    public RenameScannerTests()
    {
        _work = Path.Combine(_root, "work");
        Directory.CreateDirectory(_work);
        File.WriteAllText(Path.Combine(_work, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_work, "b.PNG"), "b");
        File.WriteAllText(Path.Combine(_work, "hidden.txt"), "h");
        File.SetAttributes(Path.Combine(_work, "hidden.txt"), FileAttributes.Hidden);
        Directory.CreateDirectory(Path.Combine(_work, "child", "grand"));
        File.WriteAllText(Path.Combine(_work, "child", "c.txt"), "c");
        File.WriteAllText(Path.Combine(_work, "child", "grand", "d.txt"), "d");
        Directory.CreateDirectory(Path.Combine(_work, "hidden-dir"));
        File.SetAttributes(Path.Combine(_work, "hidden-dir"), FileAttributes.Hidden);
        File.WriteAllText(Path.Combine(_work, "hidden-dir", "e.txt"), "e");
        _scanner = new RenameScanner(new LoggerConfiguration().CreateLogger());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_GivenFilesFirstLevel_ListsFilesAndSkippedHiddenOnes()
    {
        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: false, string.Empty));

        Assert.Equal(
            ["a.txt", "b.PNG"],
            result.Candidates
                .Where(candidate => candidate.SkipReason is null)
                .Select(candidate => candidate.RelativePath)
                .OrderBy(name => name, StringComparer.Ordinal));
        RenameCandidate hidden = Assert.Single(
            result.Candidates,
            candidate => candidate.SkipReason == RenameSkipReason.Hidden);
        Assert.Equal("hidden.txt", hidden.RelativePath);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.RelativePath.Contains("child"));
    }

    [Fact]
    public async Task ScanAsync_GivenSubdirectories_ListsNestedFiles()
    {
        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: true, string.Empty));

        Assert.Contains(result.Candidates, candidate => candidate.RelativePath == "child/c.txt");
        Assert.Contains(result.Candidates, candidate => candidate.RelativePath == "child/grand/d.txt");
        Assert.DoesNotContain(result.Candidates, candidate => candidate.RelativePath.StartsWith("hidden-dir", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScanAsync_GivenExtensionFilter_KeepsMatchingFilesOnly()
    {
        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: true, ".png"));

        Assert.Equal(
            ["b.PNG"],
            result.Candidates
                .Where(candidate => candidate.SkipReason is null)
                .Select(candidate => candidate.RelativePath));
    }

    [Fact]
    public async Task ScanAsync_GivenFolders_ListsDirectoriesWithoutTheSelectedOne()
    {
        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Folders, recurse: false, string.Empty));

        Assert.Equal(
            ["child", "hidden-dir"],
            result.Candidates.Select(candidate => candidate.RelativePath).OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(result.Candidates, candidate => candidate.FullPath == _work);
        Assert.Equal(
            RenameSkipReason.Hidden,
            result.Candidates.Single(candidate => candidate.RelativePath == "hidden-dir").SkipReason);
    }

    [Fact]
    public async Task ScanAsync_GivenFolderSubdirectories_ListsTheWholeTree()
    {
        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Folders, recurse: true, string.Empty));

        Assert.Contains(result.Candidates, candidate => candidate.RelativePath == "child/grand");
    }

    [Fact]
    public async Task ScanAsync_GivenMissingDirectory_ReturnsNoCandidates()
    {
        RenameScanResult result = await _scanner.ScanAsync(
            new RenameScanRequest(Path.Combine(_work, "missing"), RenameTarget.Files, false, string.Empty));

        Assert.Empty(result.Candidates);
    }

    private RenameScanRequest Request(RenameTarget target, bool recurse, string extensions) =>
        new(_work, target, recurse, extensions);
}
