using System.Diagnostics;

namespace JiuMi.Infrastructure.Tests;

public sealed class ShellLauncherTests
{
    [Fact]
    public void OpenFile_GivenValidPath_LaunchesWithShellExecute()
    {
        ProcessStartInfo? captured = null;
        var launcher = new ShellLauncher(info => captured = info, () => true);

        launcher.OpenFile(@"C:\Videos\clip.mp4");

        Assert.NotNull(captured);
        Assert.Equal(@"C:\Videos\clip.mp4", captured.FileName);
        Assert.True(captured.UseShellExecute);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenFile_GivenBlankPath_ThrowsArgumentException(string path)
    {
        var launcher = new ShellLauncher(_ => { }, () => true);

        Assert.Throws<ArgumentException>(() => launcher.OpenFile(path));
    }

    [Fact]
    public void RevealInFolder_OnWindows_LaunchesExplorerWithSelectArgument()
    {
        ProcessStartInfo? captured = null;
        var launcher = new ShellLauncher(info => captured = info, () => true);
        string rawPath = Path.Combine(Path.GetTempPath(), "My Videos", "clip 01.mp4");
        string expectedFullPath = Path.GetFullPath(rawPath);

        launcher.RevealInFolder(rawPath);

        Assert.NotNull(captured);
        Assert.Equal("explorer.exe", captured.FileName);
        Assert.False(captured.UseShellExecute);
        Assert.Equal($"/select,\"{expectedFullPath}\"", captured.Arguments);
    }

    [Fact]
    public void RevealInFolder_OnWindows_NormalizesForwardSlashes()
    {
        ProcessStartInfo? captured = null;
        var launcher = new ShellLauncher(info => captured = info, () => true);
        string forwardSlashPath = Path.Combine(Path.GetTempPath(), "nested", "clip.mp4").Replace('\\', '/');
        string expectedFullPath = Path.GetFullPath(forwardSlashPath);

        launcher.RevealInFolder(forwardSlashPath);

        Assert.NotNull(captured);
        Assert.Equal("explorer.exe", captured.FileName);
        Assert.Equal($"/select,\"{expectedFullPath}\"", captured.Arguments);
    }

    [Fact]
    public void RevealInFolder_OnWindows_GivenPathWithQuote_ThrowsArgumentException()
    {
        var launcher = new ShellLauncher(_ => { }, () => true);

        Assert.Throws<ArgumentException>(() => launcher.RevealInFolder(@"C:\Videos\bad""name.mp4"));
    }

    [Fact]
    public void RevealInFolder_OnNonWindows_FallsBackToParentDirectory()
    {
        ProcessStartInfo? captured = null;
        var launcher = new ShellLauncher(info => captured = info, () => false);
        string filePath = Path.Combine(Path.GetTempPath(), "My Videos", "clip.mp4");
        string? expectedDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        Assert.NotNull(expectedDirectory);

        launcher.RevealInFolder(filePath);

        Assert.NotNull(captured);
        Assert.Equal(expectedDirectory, captured.FileName);
        Assert.True(captured.UseShellExecute);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RevealInFolder_GivenBlankPath_ThrowsArgumentException(string path)
    {
        var launcher = new ShellLauncher(_ => { }, () => true);

        Assert.Throws<ArgumentException>(() => launcher.RevealInFolder(path));
    }
}
