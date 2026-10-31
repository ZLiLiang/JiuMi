namespace JiuMi.Application.FileRename;

/// <summary>
/// The unchanged parameters of one rename rule: either a search-and-replace or a whole-name template.
/// </summary>
public sealed record RenameRule
{
    /// <summary>Gets the way the rule builds a new name.</summary>
    public RenameRuleKind Kind
    {
        get;
        init;
    }

    /// <summary>Gets the text searched for when <see cref="Kind"/> is <see cref="RenameRuleKind.Replace"/>.</summary>
    public string Find { get; init; } = string.Empty;

    /// <summary>Gets the replacement text when <see cref="Kind"/> is <see cref="RenameRuleKind.Replace"/>.</summary>
    public string Replace { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether <see cref="Find"/> is a regular expression.</summary>
    public bool UseRegex
    {
        get;
        init;
    }

    /// <summary>Gets a value indicating whether <see cref="Replace"/> expands <c>{index}</c> tokens.</summary>
    public bool ExpandIndexTokens { get; init; } = true;

    /// <summary>Gets the whole-name template when <see cref="Kind"/> is <see cref="RenameRuleKind.Template"/>.</summary>
    public string Template { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the user has supplied enough input for the rule to match
    /// anything. An empty rule produces no preview rows and no errors.
    /// </summary>
    public bool HasInput => Kind switch
    {
        RenameRuleKind.Replace => Find.Length > 0,
        RenameRuleKind.Template => Template.Length > 0,
        _ => false,
    };

    /// <summary>
    /// Creates a search-and-replace rule.
    /// </summary>
    /// <param name="find">The text or pattern to find.</param>
    /// <param name="replace">The replacement text.</param>
    /// <param name="useRegex">Whether <paramref name="find"/> is a regular expression.</param>
    /// <param name="expandIndexTokens">Whether the replacement expands index tokens.</param>
    /// <returns>The rule.</returns>
    public static RenameRule CreateReplace(
        string find,
        string replace,
        bool useRegex = false,
        bool expandIndexTokens = true)
    {
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(replace);

        return new RenameRule
        {
            Kind = RenameRuleKind.Replace,
            Find = find,
            Replace = replace,
            UseRegex = useRegex,
            ExpandIndexTokens = expandIndexTokens,
        };
    }

    /// <summary>
    /// Creates a whole-name template rule.
    /// </summary>
    /// <param name="template">The template with <c>{name}</c>, <c>{index}</c>, <c>{ext}</c> and <c>{date}</c>.</param>
    /// <returns>The rule.</returns>
    public static RenameRule CreateTemplate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new RenameRule { Kind = RenameRuleKind.Template, Template = template };
    }
}
