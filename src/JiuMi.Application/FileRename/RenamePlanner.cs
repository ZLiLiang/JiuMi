using System.Text.RegularExpressions;
using JiuMi.Application.FileNames;

namespace JiuMi.Application.FileRename;

/// <summary>
/// Builds the rename preview: it sorts the candidates, numbers them, composes target names and
/// reports every reason a target cannot be used. It performs no file-system access of its own;
/// target occupancy is answered by the caller's predicate.
/// </summary>
public static class RenamePlanner
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Builds the plan for one rule.
    /// </summary>
    /// <param name="rule">The rule that composes target names.</param>
    /// <param name="candidates">Every item found under the selected directory.</param>
    /// <param name="today">The date substituted for <c>{date}</c>.</param>
    /// <param name="pathExists">Answers whether an absolute path exists on disk.</param>
    /// <returns>The immutable plan.</returns>
    public static RenamePlan Build(
        RenameRule rule,
        IReadOnlyList<RenameCandidate> candidates,
        DateOnly today,
        Func<string, bool>? pathExists = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(candidates);

        var skipped = new List<RenameSkippedItem>();
        var active = new List<RenameCandidate>(candidates.Count);
        foreach (RenameCandidate candidate in candidates)
        {
            if (candidate.SkipReason is { } reason)
            {
                skipped.Add(new RenameSkippedItem(candidate, reason));
            }
            else
            {
                active.Add(candidate);
            }
        }

        if (!rule.HasInput)
        {
            return new RenamePlan([], skipped, active.Count);
        }

        List<RenameCandidate> matched = [.. active.Where(candidate => Matches(rule, candidate))];
        matched.Sort(CompareCandidates);
        int matchedCount = matched.Count;

        List<RenamePlanEntry> entries = BuildChangedEntries(rule, matched, today);
        var targetCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (RenamePlanEntry entry in entries)
        {
            targetCounts[entry.TargetFullPath] = targetCounts.GetValueOrDefault(entry.TargetFullPath) + 1;
        }

        var validated = new List<RenamePlanEntry>(entries.Count);
        foreach (RenamePlanEntry entry in entries)
        {
            validated.Add(entry with
            {
                Error = FindError(entry, targetCounts, pathExists),
            });
        }

        return new RenamePlan(validated, skipped, active.Count - matchedCount);
    }

    private static List<RenamePlanEntry> BuildChangedEntries(
        RenameRule rule,
        IReadOnlyList<RenameCandidate> matched,
        DateOnly today)
    {
        // A row whose target equals its source is dropped. Dropping rows changes the batch size,
        // which changes index tokens and can drop more rows, so the numbering is recomputed until
        // the set is stable. Three passes cover every rule the page can express.
        var current = matched;
        List<RenamePlanEntry> entries = [];
        for (int pass = 0; pass < 3 && current.Count > 0; pass++)
        {
            int count = current.Count;
            var built = new List<RenamePlanEntry>(count);
            for (int index = 0; index < count; index++)
            {
                RenameCandidate candidate = current[index];
                string originalName = Path.GetFileName(candidate.FullPath);
                string targetName = BuildTargetName(rule, originalName, index + 1, count, today);
                if (string.Equals(targetName, originalName, StringComparison.Ordinal))
                {
                    continue;
                }

                string directory = Path.GetDirectoryName(candidate.FullPath) ?? string.Empty;
                built.Add(new RenamePlanEntry(
                    candidate,
                    index + 1,
                    targetName,
                    Path.Combine(directory, targetName),
                    Error: null,
                    ChangesExtension: !string.Equals(
                        Path.GetExtension(originalName),
                        Path.GetExtension(targetName),
                        StringComparison.Ordinal)));
            }

            entries = built;
            if (built.Count == count)
            {
                break;
            }

            current = [.. built.Select(entry => entry.Candidate)];
        }

        return entries;
    }

    private static RenamePrecheckError? FindError(
        RenamePlanEntry entry,
        Dictionary<string, int> targetCounts,
        Func<string, bool>? pathExists)
    {
        FileNameIssue issue = FileNameRules.FindIssue(entry.TargetName, entry.TargetFullPath);
        if (issue != FileNameIssue.None)
        {
            return issue switch
            {
                FileNameIssue.Empty => RenamePrecheckError.EmptyName,
                FileNameIssue.IllegalCharacter => RenamePrecheckError.IllegalCharacter,
                FileNameIssue.TrailingSpaceOrDot => RenamePrecheckError.TrailingSpaceOrDot,
                FileNameIssue.ReservedName => RenamePrecheckError.ReservedName,
                _ => RenamePrecheckError.TooLong,
            };
        }

        if (targetCounts.TryGetValue(entry.TargetFullPath, out int count) && count > 1)
        {
            return RenamePrecheckError.Duplicate;
        }

        return pathExists is not null &&
            pathExists(entry.TargetFullPath) &&
            !string.Equals(entry.TargetFullPath, entry.Candidate.FullPath, StringComparison.OrdinalIgnoreCase)
            ? RenamePrecheckError.Occupied
            : null;
    }

    private static string BuildTargetName(
        RenameRule rule,
        string originalName,
        int index,
        int count,
        DateOnly today)
    {
        if (rule.Kind == RenameRuleKind.Template)
        {
            (string stem, string extension) = FileNameRules.SplitExtension(originalName);
            return RenameTokens.ExpandTemplate(rule.Template, stem, extension, index, count, today);
        }

        string replacement = rule.ExpandIndexTokens
            ? RenameTokens.ExpandIndex(rule.Replace, index, count)
            : rule.Replace;

        if (!rule.UseRegex)
        {
            return originalName.Replace(rule.Find, replacement, StringComparison.Ordinal);
        }

        try
        {
            return Regex.Replace(
                originalName,
                rule.Find,
                _ => replacement,
                RegexOptions.None,
                _regexTimeout);
        }
        catch (ArgumentException)
        {
            return originalName;
        }
        catch (RegexMatchTimeoutException)
        {
            return originalName;
        }
    }

    private static bool Matches(RenameRule rule, RenameCandidate candidate)
    {
        string name = Path.GetFileName(candidate.FullPath);
        if (rule.Kind == RenameRuleKind.Template)
        {
            return rule.Template.Length > 0;
        }

        if (!rule.UseRegex)
        {
            return name.Contains(rule.Find, StringComparison.Ordinal);
        }

        try
        {
            return Regex.IsMatch(name, rule.Find, RegexOptions.None, _regexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static int CompareCandidates(RenameCandidate left, RenameCandidate right)
    {
        if (left.IsDirectory || right.IsDirectory)
        {
            int depthOrder = Depth(right.RelativePath).CompareTo(Depth(left.RelativePath));
            if (depthOrder != 0)
            {
                return depthOrder;
            }
        }

        int nameOrder = NaturalStringComparer.Instance.Compare(left.RelativePath, right.RelativePath);
        return nameOrder != 0 ? nameOrder : string.CompareOrdinal(left.FullPath, right.FullPath);
    }

    private static int Depth(string relativePath) => relativePath.Count(character => character == '/');
}
