namespace JiuMi.Application.FileRename;

/// <summary>
/// Converts a basic task into the same <see cref="RenameRule"/> the advanced level writes by hand,
/// so numbering, sorting, precheck and preview have one implementation.
/// </summary>
public static class RenameRuleConverter
{
    /// <summary>The zero-pad widths the basic sequence task offers.</summary>
    public static readonly IReadOnlyList<int> SequencePadWidths = [0, 2, 3, 4];

    /// <summary>
    /// Converts a basic task into a rule.
    /// </summary>
    /// <param name="task">The chosen basic task.</param>
    /// <param name="find">The literal text to find, for <see cref="RenameBasicTask.Replace"/>.</param>
    /// <param name="replace">The literal replacement, for <see cref="RenameBasicTask.Replace"/>.</param>
    /// <param name="position">Where the affix text goes, for <see cref="RenameBasicTask.Affix"/>.</param>
    /// <param name="affixText">The affix text, for <see cref="RenameBasicTask.Affix"/>.</param>
    /// <param name="sequenceName">The name before the number, for <see cref="RenameBasicTask.Sequence"/>.</param>
    /// <param name="padWidth">The zero-pad width, for <see cref="RenameBasicTask.Sequence"/>.</param>
    /// <returns>The equivalent rule.</returns>
    public static RenameRule FromBasic(
        RenameBasicTask task,
        string find,
        string replace,
        RenameAffixPosition position,
        string affixText,
        string sequenceName,
        int padWidth)
    {
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(replace);
        ArgumentNullException.ThrowIfNull(affixText);
        ArgumentNullException.ThrowIfNull(sequenceName);

        return task switch
        {
            RenameBasicTask.Replace => RenameRule.CreateReplace(
                find,
                replace,
                useRegex: false,
                expandIndexTokens: false),
            RenameBasicTask.Affix => RenameRule.CreateTemplate(
                position == RenameAffixPosition.Prefix
                    ? affixText + "{name}{ext}"
                    : "{name}" + affixText + "{ext}"),
            RenameBasicTask.Sequence => RenameRule.CreateTemplate(
                sequenceName + RenameTokens.IndexToken(Math.Max(padWidth, 0)) + "{ext}"),
            _ => throw new ArgumentOutOfRangeException(nameof(task), task, null),
        };
    }
}
