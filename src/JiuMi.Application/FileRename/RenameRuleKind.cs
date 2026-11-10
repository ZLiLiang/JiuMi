namespace JiuMi.Application.FileRename;

/// <summary>
/// The two ways a rename rule can be written. Both share numbering, sorting, precheck and preview.
/// </summary>
public enum RenameRuleKind
{
    /// <summary>Replace a literal or regular expression inside the name.</summary>
    Replace,

    /// <summary>Compose the whole name from a template.</summary>
    Template,
}
