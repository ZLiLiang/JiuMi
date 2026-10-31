namespace JiuMi.Application.FileRename;

/// <summary>
/// The rename parameters kept between sessions. Both rule levels are stored so switching levels
/// never loses input; the selected directory is deliberately not stored.
/// </summary>
public sealed record FileRenameSettings
{
    /// <summary>Gets the level shown when the page opens.</summary>
    public RenameRuleLevel RuleLevel { get; init; } = RenameRuleLevel.Basic;

    /// <summary>Gets the basic task shown when the page opens.</summary>
    public RenameBasicTask BasicTask { get; init; } = RenameBasicTask.Replace;

    /// <summary>Gets the basic literal text to find.</summary>
    public string BasicFind { get; init; } = string.Empty;

    /// <summary>Gets the basic literal replacement.</summary>
    public string BasicReplace { get; init; } = string.Empty;

    /// <summary>Gets where the basic affix task puts its text.</summary>
    public RenameAffixPosition AffixPosition { get; init; } = RenameAffixPosition.Prefix;

    /// <summary>Gets the basic affix text.</summary>
    public string AffixText { get; init; } = string.Empty;

    /// <summary>Gets the basic sequence name typed by the user; empty means the localized default.</summary>
    public string SequenceName { get; init; } = string.Empty;

    /// <summary>Gets the basic sequence zero-pad width: 0, 2, 3 or 4.</summary>
    public int SequencePadWidth
    {
        get;
        init;
    }

    /// <summary>Gets the advanced rule kind.</summary>
    public RenameRuleKind AdvancedKind { get; init; } = RenameRuleKind.Replace;

    /// <summary>Gets the advanced search text or pattern.</summary>
    public string AdvancedFind { get; init; } = string.Empty;

    /// <summary>Gets the advanced replacement text.</summary>
    public string AdvancedReplace { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the advanced search text is a regular expression.</summary>
    public bool AdvancedUseRegex
    {
        get;
        init;
    }

    /// <summary>Gets the advanced whole-name template.</summary>
    public string Template { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether subdirectories are scanned.</summary>
    public bool IncludeSubdirectories
    {
        get;
        init;
    }

    /// <summary>Gets the extension filter text typed by the user.</summary>
    public string Extensions { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a safety copy is made before renaming.</summary>
    public bool BackupEnabled
    {
        get;
        init;
    }

    /// <summary>
    /// Creates the settings used before the user changes anything.
    /// </summary>
    /// <returns>The defaults: basic level, literal replace, first level, no backup.</returns>
    public static FileRenameSettings CreateDefault() => new();
}
