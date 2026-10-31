using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenamePlannerTests
{
    private const string Root = @"C:\work";
    private static readonly DateOnly _today = new(2026, 10, 31);

    [Fact]
    public void Build_GivenLiteralReplace_ReplacesEveryOccurrenceAndSortsNaturally()
    {
        RenamePlan plan = Build(
            RenameRule.CreateReplace("产品图-0", "product-", expandIndexTokens: false),
            Files("产品图-02.png", "产品图-01.png", "会议记录.txt"));

        Assert.Equal(["product-1.png", "product-2.png"], plan.Entries.Select(entry => entry.TargetName));
        Assert.Equal([1, 2], plan.Entries.Select(entry => entry.Index));
        Assert.Equal(1, plan.UnmatchedCount);
        Assert.True(plan.CanExecute);
    }

    [Fact]
    public void Build_GivenTemplate_ComposesTheWholeNameAndNumbersInNaturalOrder()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{index:00}-{name}{ext}"),
            Files("10.txt", "2.txt", "a.txt"));

        Assert.Equal(["01-2.txt", "02-10.txt", "03-a.txt"], plan.Entries.Select(entry => entry.TargetName));
    }

    [Fact]
    public void Build_GivenRegexReplace_TreatsReplacementAsLiteralText()
    {
        RenamePlan plan = Build(
            RenameRule.CreateReplace(@"^产品图-\d+", "$1", useRegex: true),
            Files("产品图-01.png"));

        Assert.Equal(["$1.png"], plan.Entries.Select(entry => entry.TargetName));
    }

    [Fact]
    public void Build_GivenRegexWithIndexToken_ExpandsTheCurrentNumber()
    {
        RenamePlan plan = Build(
            RenameRule.CreateReplace(@"^产品图", "-{index:00}-", useRegex: true),
            Files("产品图-02.png", "产品图-01.png"));

        Assert.Equal(["-01--01.png", "-02--02.png"], plan.Entries.Select(entry => entry.TargetName));
    }

    [Fact]
    public void Build_GivenTwoRowsWithTheSameTarget_ReportsDuplicateOnBoth()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("same{ext}"),
            Files("a.txt", "b.txt"));

        Assert.Equal(2, plan.ConflictCount);
        Assert.All(plan.Entries, entry => Assert.Equal(RenamePrecheckError.Duplicate, entry.Error));
        Assert.False(plan.CanExecute);
    }

    [Fact]
    public void Build_GivenExistingTarget_ReportsOccupied()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(Root, "taken.txt"),
        };

        RenamePlan plan = Build(
            RenameRule.CreateTemplate("taken{ext}"),
            Files("a.txt"),
            existing.Contains);

        Assert.Equal(RenamePrecheckError.Occupied, Assert.Single(plan.Entries).Error);
    }

    [Fact]
    public void Build_GivenCaseOnlyTarget_DoesNotReportTheSourceAsOccupied()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(Root, "report.txt"),
        };

        RenamePlan plan = Build(
            RenameRule.CreateTemplate("REPORT{ext}"),
            Files("report.txt"),
            existing.Contains);

        Assert.Null(Assert.Single(plan.Entries).Error);
    }

    [Theory]
    [InlineData("a:b{ext}", RenamePrecheckError.IllegalCharacter)]
    [InlineData("{name} ", RenamePrecheckError.TrailingSpaceOrDot)]
    [InlineData("CON{ext}", RenamePrecheckError.ReservedName)]
    public void Build_GivenInvalidTargetName_ReportsThePrecheckError(
        string template,
        RenamePrecheckError expected)
    {
        RenamePlan plan = Build(RenameRule.CreateTemplate(template), Files("a.txt"));

        Assert.Equal(expected, Assert.Single(plan.Entries).Error);
    }

    [Fact]
    public void Build_GivenTargetNameAboveTheLimit_ReportsTooLong()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate(new string('a', 256) + "{ext}"),
            Files("a.txt"));

        Assert.Equal(RenamePrecheckError.TooLong, Assert.Single(plan.Entries).Error);
    }

    [Fact]
    public void Build_GivenSkippedCandidate_ListsItAndLeavesItOutOfTheRows()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{index}{ext}"),
            [
                new RenameCandidate(Path.Combine(Root, "a.txt"), "a.txt", IsDirectory: false),
                new RenameCandidate(
                    Path.Combine(Root, "secret.txt"),
                    "secret.txt",
                    IsDirectory: false,
                    RenameSkipReason.Hidden),
            ]);

        Assert.Single(plan.Entries);
        RenameSkippedItem skipped = Assert.Single(plan.Skipped);
        Assert.Equal("secret.txt", skipped.Candidate.RelativePath);
        Assert.Equal(RenameSkipReason.Hidden, skipped.Reason);
    }

    [Theory]
    [InlineData(RenameSkipReason.System)]
    [InlineData(RenameSkipReason.SymbolicLink)]
    [InlineData(RenameSkipReason.Junction)]
    public void Build_GivenSkipReason_ListsItAndLeavesItOutOfTheRows(RenameSkipReason reason)
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{index}{ext}"),
            [
                new RenameCandidate(Path.Combine(Root, "a.txt"), "a.txt", IsDirectory: false),
                new RenameCandidate(Path.Combine(Root, "linked.txt"), "linked.txt", IsDirectory: false, reason),
            ]);

        Assert.Single(plan.Entries);
        RenameSkippedItem skipped = Assert.Single(plan.Skipped);
        Assert.Equal("linked.txt", skipped.Candidate.RelativePath);
        Assert.Equal(reason, skipped.Reason);
    }

    [Fact]
    public void Build_GivenFolders_NumbersDeepestFirstThenByName()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{index}-{name}"),
            [
                Folder("A"),
                Folder("A/B"),
                Folder("A/B/C"),
                Folder("D"),
            ]);

        Assert.Equal(
            ["1-C", "2-B", "3-A", "4-D"],
            plan.Entries.Select(entry => entry.TargetName));
    }

    [Fact]
    public void Build_GivenFolderCandidates_PlacesTheTargetBesideTheSource()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("x-{name}"),
            [Folder("A/B")]);

        Assert.Equal(Path.Combine(Root, "A", "x-B"), Assert.Single(plan.Entries).TargetFullPath);
    }

    [Fact]
    public void Build_GivenTemplateWithoutExtension_CountsTheExtensionChange()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{name}"),
            Files("a.png", "b.txt"));

        Assert.Equal(2, plan.ExtensionChangeCount);
        Assert.All(plan.Entries, entry => Assert.True(entry.ChangesExtension));
    }

    [Fact]
    public void Build_GivenTemplateWithSameExtension_DoesNotCountAChange()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{name}-1{ext}"),
            Files("a.png"));

        Assert.Equal(0, plan.ExtensionChangeCount);
    }

    [Fact]
    public void Build_GivenCaseOnlyExtensionChange_CountsTheChange()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{name}.PNG"),
            Files("a.png"));

        Assert.True(Assert.Single(plan.Entries).ChangesExtension);
    }

    [Fact]
    public void Build_GivenEmptyFind_ReturnsNoRowsAndNoErrors()
    {
        RenamePlan plan = Build(
            RenameRule.CreateReplace(string.Empty, "x"),
            Files("a.txt", "b.txt"));

        Assert.Empty(plan.Entries);
        Assert.Equal(2, plan.UnmatchedCount);
        Assert.False(plan.CanExecute);
    }

    [Fact]
    public void Build_GivenEmptyTemplate_ReturnsNoRowsAndNoErrors()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate(string.Empty),
            Files("a.txt"));

        Assert.Empty(plan.Entries);
        Assert.Equal(1, plan.UnmatchedCount);
    }

    [Fact]
    public void Build_GivenRuleThatLeavesNamesUnchanged_ReturnsNoRows()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("{name}{ext}"),
            Files("a.txt", "b.txt"));

        Assert.Empty(plan.Entries);
        Assert.Equal(0, plan.UnmatchedCount);
    }

    [Fact]
    public void Build_GivenInvalidRegex_ReturnsNoRowsInsteadOfThrowing()
    {
        RenamePlan plan = Build(
            RenameRule.CreateReplace("(", "x", useRegex: true),
            Files("a.txt"));

        Assert.Empty(plan.Entries);
    }

    [Fact]
    public void Build_GivenUnmatchedCandidates_CountsThemForTheFooter()
    {
        RenamePlan plan = Build(
            RenameRule.CreateReplace("alpha", "x", expandIndexTokens: false),
            Files("alpha.txt", "beta.txt"));

        Assert.Single(plan.Entries);
        Assert.Equal(1, plan.UnmatchedCount);
    }

    [Fact]
    public void Build_GivenConflicts_DisablesExecution()
    {
        RenamePlan plan = Build(
            RenameRule.CreateTemplate("a:b"),
            Files("a.txt", "b.txt"));

        Assert.True(plan.HasConflicts);
        Assert.False(plan.CanExecute);
        Assert.Equal([RenamePrecheckError.IllegalCharacter], plan.DistinctErrors);
    }

    private static RenamePlan Build(
        RenameRule rule,
        IReadOnlyList<RenameCandidate> candidates,
        Func<string, bool>? pathExists = null) =>
        RenamePlanner.Build(rule, candidates, _today, pathExists);

    private static IReadOnlyList<RenameCandidate> Files(params string[] names) =>
        [.. names.Select(name => new RenameCandidate(
            Path.Combine(Root, name),
            name,
            IsDirectory: false))];

    private static RenameCandidate Folder(string relativePath) =>
        new(
            Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            relativePath,
            IsDirectory: true);
}
