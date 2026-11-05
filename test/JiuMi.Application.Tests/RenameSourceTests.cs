using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenameSourceTests
{
    [Fact]
    public void BackupGroup_GivenAFolderSource_UsesTheDirectoryName()
    {
        var source = new RenameSource(2, RenameSourceKind.Folder, [@"D:\照片"]);

        Assert.Equal(@"D:\照片", source.Directory);
        Assert.Equal("2-照片", source.BackupGroup);
    }

    [Fact]
    public void BackupGroup_GivenAFileSource_UsesTheFirstFilesDirectoryName()
    {
        var source = new RenameSource(
            3,
            RenameSourceKind.Files,
            [@"D:\素材\a.png", @"E:\elsewhere\b.png"]);

        Assert.Equal(@"D:\素材", source.Directory);
        Assert.Equal("3-素材", source.BackupGroup);
    }

    [Fact]
    public void BackupGroup_GivenADriveRoot_UsesTheDriveLetter()
    {
        var source = new RenameSource(1, RenameSourceKind.Folder, [@"D:\"]);

        Assert.Equal("1-D", source.BackupGroup);
    }

    [Fact]
    public void BackupRoot_GivenAFolderSource_IsTheParentDirectory()
    {
        var source = new RenameSource(1, RenameSourceKind.Folder, [@"D:\photos\trip"]);

        Assert.Equal(@"D:\photos", source.BackupRoot);
    }

    [Fact]
    public void BackupRoot_GivenAFileSource_IsTheParentOfTheFirstFilesDirectory()
    {
        var source = new RenameSource(
            1,
            RenameSourceKind.Files,
            [@"D:\photos\a.png", @"E:\elsewhere\b.png"]);

        Assert.Equal(@"D:\", source.BackupRoot);
    }

    [Fact]
    public void BackupRoot_GivenADriveRoot_IsNull()
    {
        var source = new RenameSource(1, RenameSourceKind.Folder, [@"D:\"]);

        Assert.Null(source.BackupRoot);
    }

    [Fact]
    public void Equals_GivenTwoSourcesWithTheSameContents_ReturnsTrue()
    {
        var first = new RenameSource(1, RenameSourceKind.Files, [@"D:\a.png", @"D:\b.png"]);
        var second = new RenameSource(1, RenameSourceKind.Files, [@"D:\a.png", @"D:\b.png"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Constructor_GivenAFolderSourceWithTwoDirectories_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RenameSource(1, RenameSourceKind.Folder, [@"C:\one", @"C:\two"]));
    }

    [Fact]
    public void Constructor_GivenNoPaths_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RenameSource(1, RenameSourceKind.Files, []));
    }
}
