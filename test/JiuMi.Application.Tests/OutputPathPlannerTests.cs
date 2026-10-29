using JiuMi.Application.ImageConversion;

namespace JiuMi.Application.Tests;

public sealed class OutputPathPlannerTests
{
    private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);

    private OutputPathPlanner CreatePlanner() => new(_existing.Contains);

    [Fact]
    public void Plan_GivenFreeName_UsesSourceNameWithTargetExtension()
    {
        OutputPathPlanner planner = CreatePlanner();

        string path = planner.Plan(@"C:\work\a.psd", outputDirectory: null, ImageFormat.Png);

        Assert.Equal(@"C:\work\a.png", path);
    }

    [Fact]
    public void Plan_GivenExistingSource_AppendsSuffixInsteadOfOverwritingIt()
    {
        _existing.Add(@"C:\work\a.png");

        string path = CreatePlanner().Plan(@"C:\work\a.png", outputDirectory: null, ImageFormat.Png);

        Assert.Equal(@"C:\work\a-1.png", path);
    }

    [Fact]
    public void Plan_GivenConsecutiveNames_SkipsEveryTakenSuffix()
    {
        _existing.Add(@"C:\work\a.png");
        _existing.Add(@"C:\work\a-1.png");
        _existing.Add(@"C:\work\a-2.png");

        string path = CreatePlanner().Plan(@"C:\work\a.psd", @"C:\work", ImageFormat.Png);

        Assert.Equal(@"C:\work\a-3.png", path);
    }

    [Fact]
    public void Plan_GivenReservedPathInBatch_SkipsItEvenWhenItDoesNotExistYet()
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            @"C:\out\a.png",
        };

        string path = CreatePlanner().Plan(@"C:\work\a.psd", @"C:\out", ImageFormat.Png, reserved);

        Assert.Equal(@"C:\out\a-1.png", path);
    }

    [Fact]
    public void Plan_GivenReservedPathInDifferentCase_TreatsItAsTaken()
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            @"C:\out\A.PNG",
        };

        string path = CreatePlanner().Plan(@"C:\work\a.psd", @"C:\out", ImageFormat.Png, reserved);

        Assert.Equal(@"C:\out\a-1.png", path);
    }

    [Fact]
    public void Plan_GivenOutputDirectory_WritesThereInsteadOfTheSourceDirectory()
    {
        string path = CreatePlanner().Plan(@"C:\work\a.psd", @"D:\exports", ImageFormat.WebP);

        Assert.Equal(@"D:\exports\a.webp", path);
    }

    [Fact]
    public void Plan_GivenRelativeSourceWithoutDirectory_Throws()
    {
        OutputPathPlanner planner = CreatePlanner();

        Assert.Throws<ArgumentException>(() => planner.Plan("a.psd", outputDirectory: null, ImageFormat.Png));
    }

    [Fact]
    public void Plan_GivenSourceNameWithInvalidFileNameCharacters_Throws()
    {
        OutputPathPlanner planner = CreatePlanner();

        Assert.Throws<ArgumentException>(() => planner.Plan(@"C:\work\a<b.psd", outputDirectory: null, ImageFormat.Png));
    }

    [Fact]
    public void Plan_GivenNonOutputFormat_Throws()
    {
        OutputPathPlanner planner = CreatePlanner();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => planner.Plan(@"C:\work\a.psd", outputDirectory: null, ImageFormat.Psd));
    }
}
