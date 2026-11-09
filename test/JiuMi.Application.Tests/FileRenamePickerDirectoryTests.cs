using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class FileRenamePickerDirectoryTests
{
    [Fact]
    public void Directory_Initially_IsNull()
    {
        var memory = new FileRenamePickerDirectory();

        Assert.Null(memory.Directory);
    }

    [Fact]
    public void Remember_GivenDirectory_MakesItTheRememberedDirectory()
    {
        var memory = new FileRenamePickerDirectory();

        bool changed = memory.Remember(@"D:\a");

        Assert.True(changed);
        Assert.Equal(@"D:\a", memory.Directory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Remember_GivenBlank_KeepsTheEarlierDirectory(string? blank)
    {
        var memory = new FileRenamePickerDirectory();
        memory.Remember(@"D:\a");

        bool changed = memory.Remember(blank);

        Assert.False(changed);
        Assert.Equal(@"D:\a", memory.Directory);
    }

    [Fact]
    public void Remember_GivenTheSameDirectory_ReportsNoChange()
    {
        var memory = new FileRenamePickerDirectory();
        memory.Remember(@"D:\a");

        bool changed = memory.Remember(@"d:\A");

        Assert.False(changed);
        Assert.Equal(@"D:\a", memory.Directory);
    }

    [Fact]
    public void Remember_GivenAnotherDirectory_ReplacesTheEarlierOne()
    {
        var memory = new FileRenamePickerDirectory();
        memory.Remember(@"D:\a");

        bool changed = memory.Remember(@"D:\b");

        Assert.True(changed);
        Assert.Equal(@"D:\b", memory.Directory);
    }
}
