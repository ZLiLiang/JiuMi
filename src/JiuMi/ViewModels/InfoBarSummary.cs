namespace JiuMi.ViewModels;

/// <summary>
/// A completed-operation notice for the global info bar: a message, a severity and an optional
/// folder the action opens. It is not an error record and carries no copyable details.
/// </summary>
/// <param name="Message">The localized summary text.</param>
/// <param name="IsWarning">Whether the summary reports failures; otherwise it is a success.</param>
/// <param name="FolderPath">The folder the action opens, or <see langword="null"/> for no action.</param>
public sealed record InfoBarSummary(string Message, bool IsWarning, string? FolderPath)
{
    /// <summary>
    /// Gets the optional second line shown below the message, or <see langword="null"/>.
    /// </summary>
    public string? SecondaryMessage
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the optional label of the folder action, or <see langword="null"/> for the default label.
    /// </summary>
    public string? ActionText
    {
        get;
        init;
    }

    /// <summary>
    /// Gets an optional action run instead of opening <see cref="FolderPath"/>.
    /// </summary>
    public Func<Task>? Action
    {
        get;
        init;
    }
}
