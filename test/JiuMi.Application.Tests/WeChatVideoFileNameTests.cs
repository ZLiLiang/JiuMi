using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoFileNameTests : IDisposable
{
    private static readonly DateTimeOffset _capturedAt =
        new(2026, 11, 16, 8, 9, 10, TimeSpan.FromHours(8));

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "jiumi-96-" + Guid.NewGuid().ToString("N"));

    public WeChatVideoFileNameTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void CreateStem_GivenIllegalCharacters_ReplacesThem()
    {
        string stem = WeChatVideoFileName.CreateStem("a<b>c:d\"e/f\\g|h?i*j", _capturedAt);

        Assert.Equal("a_b_c_d_e_f_g_h_i_j", stem);
    }

    [Fact]
    public void CreateStem_GivenControlCharacters_ReplacesThem()
    {
        string stem = WeChatVideoFileName.CreateStem("line\r\nbreak\ttab", _capturedAt);

        Assert.Equal("line__break_tab", stem);
    }

    [Fact]
    public void CreateStem_GivenSurroundingWhitespaceAndTrailingDots_TrimsThem()
    {
        string stem = WeChatVideoFileName.CreateStem("  hello. . .  ", _capturedAt);

        Assert.Equal("hello", stem);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("com1.txt")]
    [InlineData("LPT9")]
    public void CreateStem_GivenReservedName_PrefixesUnderscore(string description)
    {
        string stem = WeChatVideoFileName.CreateStem(description, _capturedAt);

        Assert.Equal("_" + description, stem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void CreateStem_GivenEmptyDescription_FallsBackToTimestamp(string? description)
    {
        string stem = WeChatVideoFileName.CreateStem(description, _capturedAt);

        Assert.Equal("视频_20261116_080910", stem);
    }

    [Fact]
    public void CreateStem_GivenLongDescription_TruncatesToOneHundredTwentyCharacters()
    {
        string stem = WeChatVideoFileName.CreateStem(new string('a', 200), _capturedAt);

        Assert.Equal(120, stem.Length);
        Assert.Equal(new string('a', 120), stem);
    }

    [Fact]
    public void ResolveUniqueStem_GivenNoCollision_ReturnsTheStem()
    {
        string stem = WeChatVideoFileName.ResolveUniqueStem(_directory, "clip");

        Assert.Equal("clip", stem);
    }

    [Fact]
    public void ResolveUniqueStem_GivenFinalAndPartCollisions_IncrementsWithoutOverwriting()
    {
        string existing = Path.Combine(_directory, "clip.mp4");
        string existingPart = Path.Combine(_directory, "clip-1.mp4.part");
        File.WriteAllText(existing, "existing");
        File.WriteAllText(existingPart, "part");

        string stem = WeChatVideoFileName.ResolveUniqueStem(_directory, "clip");

        Assert.Equal("clip-2", stem);
        Assert.Equal("existing", File.ReadAllText(existing));
        Assert.Equal("part", File.ReadAllText(existingPart));
    }

    [Fact]
    public void ResolveUniqueStem_GivenLongStem_KeepsTheResultWithinTheLimit()
    {
        string longStem = new('a', WeChatVideoFileName.MaxStemLength);
        File.WriteAllText(Path.Combine(_directory, longStem + ".mp4"), "existing");

        string stem = WeChatVideoFileName.ResolveUniqueStem(_directory, longStem);

        Assert.Equal(WeChatVideoFileName.MaxStemLength, stem.Length);
        Assert.EndsWith("-1", stem, StringComparison.Ordinal);
    }
}
