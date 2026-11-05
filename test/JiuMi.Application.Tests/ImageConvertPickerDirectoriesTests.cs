using JiuMi.Application.ImageConversion;

namespace JiuMi.Application.Tests;

public sealed class ImageConvertPickerDirectoriesTests
{
    [Fact]
    public void SourceDirectory_Initially_IsNull()
    {
        var memory = new ImageConvertPickerDirectories();

        Assert.Null(memory.SourceDirectory);
    }

    [Fact]
    public void Remember_GivenDirectory_MakesItTheSourceDirectory()
    {
        var memory = new ImageConvertPickerDirectories();

        bool changed = memory.Remember(@"C:\素材");

        Assert.True(changed);
        Assert.Equal(@"C:\素材", memory.SourceDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Remember_GivenBlank_KeepsTheEarlierDirectory(string? blank)
    {
        var memory = new ImageConvertPickerDirectories();
        memory.Remember(@"C:\素材");

        bool changed = memory.Remember(blank);

        Assert.False(changed);
        Assert.Equal(@"C:\素材", memory.SourceDirectory);
    }

    [Fact]
    public void Remember_GivenTheSameDirectory_ReportsNoChange()
    {
        var memory = new ImageConvertPickerDirectories();
        memory.Remember(@"C:\素材");

        bool changed = memory.Remember(@"c:\素材");

        Assert.False(changed);
        Assert.Equal(@"C:\素材", memory.SourceDirectory);
    }

    [Fact]
    public void GetOutputDirectory_GivenCustomOutputAndRememberedDirectory_ReturnsTheCustomOutput()
    {
        var memory = new ImageConvertPickerDirectories();
        memory.Remember(@"C:\素材");

        string? start = memory.GetOutputDirectory(@"D:\out");

        Assert.Equal(@"D:\out", start);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetOutputDirectory_GivenNoCustomOutput_ReturnsTheRememberedDirectory(string? customOutput)
    {
        var memory = new ImageConvertPickerDirectories();
        memory.Remember(@"C:\素材");

        string? start = memory.GetOutputDirectory(customOutput);

        Assert.Equal(@"C:\素材", start);
    }

    [Fact]
    public void GetOutputDirectory_GivenNeitherDirectory_ReturnsNull()
    {
        var memory = new ImageConvertPickerDirectories();

        string? start = memory.GetOutputDirectory(customOutputDirectory: null);

        Assert.Null(start);
    }
}
