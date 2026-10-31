using System.Diagnostics;
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
        File.WriteAllText(Path.Combine(_work, "system.txt"), "s");
        File.SetAttributes(Path.Combine(_work, "system.txt"), FileAttributes.System);
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
            DeleteTree(_root);
        }
    }

    private static void DeleteTree(string path)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(path))
        {
            FileAttributes attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.Directory) &&
                !attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                DeleteTree(entry);
                continue;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                Directory.Delete(entry, recursive: false);
            }
            else
            {
                File.SetAttributes(entry, FileAttributes.Normal);
                File.Delete(entry);
            }
        }

        Directory.Delete(path, recursive: false);
    }

    [Fact]
    public async Task ScanAsync_GivenSystemFile_ListsItAsSystemSkipped()
    {
        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: false, string.Empty));

        RenameCandidate system = Assert.Single(result.Candidates, candidate => candidate.RelativePath == "system.txt");
        Assert.Equal(RenameSkipReason.System, system.SkipReason);
    }

    [Fact]
    public async Task ScanAsync_GivenReadOnlyFile_DoesNotSkipIt()
    {
        File.SetAttributes(Path.Combine(_work, "a.txt"), FileAttributes.ReadOnly);

        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: false, string.Empty));

        RenameCandidate file = Assert.Single(result.Candidates, candidate => candidate.RelativePath == "a.txt");
        Assert.Null(file.SkipReason);
    }

    [Fact]
    public async Task ScanAsync_GivenDirectoryJunction_SkipsItAndDoesNotFollowIt()
    {
        string external = Path.Combine(_root, "external");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "outside.txt"), "outside");
        string junction = Path.Combine(_work, "junction-dir");
        if (!TryCreateJunction(junction, external))
        {
            return;
        }

        RenameScanResult files = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: true, string.Empty));
        Assert.DoesNotContain(files.Candidates, candidate => candidate.RelativePath.Contains("outside.txt", StringComparison.Ordinal));

        RenameScanResult folders = await _scanner.ScanAsync(Request(RenameTarget.Folders, recurse: false, string.Empty));
        RenameCandidate link = Assert.Single(folders.Candidates, candidate => candidate.RelativePath == "junction-dir");
        Assert.Equal(RenameSkipReason.Junction, link.SkipReason);
    }

    [Fact]
    public async Task ScanAsync_GivenSymbolicLinkFile_SkipsItWhenCreationIsAllowed()
    {
        string target = Path.Combine(_work, "a.txt");
        string link = Path.Combine(_work, "link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            // Creating symbolic links needs a privilege this machine may not grant; the junction
            // test still covers reparse-point skipping.
            return;
        }

        RenameScanResult result = await _scanner.ScanAsync(Request(RenameTarget.Files, recurse: false, string.Empty));

        RenameCandidate file = Assert.Single(result.Candidates, candidate => candidate.RelativePath == "link.txt");
        Assert.Equal(RenameSkipReason.SymbolicLink, file.SkipReason);
    }

    private static bool TryCreateJunction(string junction, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(junction);
        startInfo.ArgumentList.Add(target);

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.WaitForExit();
        return process.ExitCode == 0;
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
