using JiuMi.Application.FileNames;

namespace JiuMi.Application.Tests;

public sealed class FileNameRulesTests
{
    [Theory]
    [InlineData("report.txt", FileNameIssue.None)]
    [InlineData("报告 2026.png", FileNameIssue.None)]
    [InlineData(".hidden", FileNameIssue.None)]
    [InlineData("CON", FileNameIssue.ReservedName)]
    [InlineData("con.txt", FileNameIssue.ReservedName)]
    [InlineData("PRN.tar.gz", FileNameIssue.ReservedName)]
    [InlineData("COM1.log", FileNameIssue.ReservedName)]
    [InlineData("lpt9", FileNameIssue.ReservedName)]
    [InlineData("COM0.txt", FileNameIssue.None)]
    [InlineData("LPT10.txt", FileNameIssue.None)]
    [InlineData("a<b.txt", FileNameIssue.IllegalCharacter)]
    [InlineData("a:b.txt", FileNameIssue.IllegalCharacter)]
    [InlineData("a\u0001b.txt", FileNameIssue.IllegalCharacter)]
    [InlineData("trailing ", FileNameIssue.TrailingSpaceOrDot)]
    [InlineData("trailing.", FileNameIssue.TrailingSpaceOrDot)]
    [InlineData("", FileNameIssue.Empty)]
    public void FindIssue_GivenName_ReturnsFirstProblem(string name, FileNameIssue expected)
    {
        Assert.Equal(expected, FileNameRules.FindIssue(name));
    }

    [Fact]
    public void FindIssue_GivenTooLongPath_ReportsTooLong()
    {
        string name = new('a', 200);
        string fullPath = @"C:\" + new string('b', FileNameRules.MaxPathLength);

        Assert.Equal(FileNameIssue.TooLong, FileNameRules.FindIssue(name, fullPath));
    }

    [Fact]
    public void FindIssue_GivenNameAbove255Characters_ReportsTooLong()
    {
        string name = new('a', FileNameRules.MaxNameLength + 1);

        Assert.Equal(FileNameIssue.TooLong, FileNameRules.FindIssue(name));
    }

    [Theory]
    [InlineData("report.txt", "report", ".txt")]
    [InlineData("archive.tar.gz", "archive.tar", ".gz")]
    [InlineData(".gitignore", ".gitignore", "")]
    [InlineData("name.", "name.", "")]
    public void SplitExtension_GivenName_SplitsAtTheLastDot(
        string name,
        string expectedStem,
        string expectedExtension)
    {
        Assert.Equal((expectedStem, expectedExtension), FileNameRules.SplitExtension(name));
    }
}
