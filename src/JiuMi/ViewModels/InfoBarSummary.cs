namespace JiuMi.ViewModels;

/// <summary>
/// A completed-operation notice for the global info bar: a message, a severity and an optional
/// folder the action opens. It is not an error record and carries no copyable details.
/// </summary>
/// <param name="Message">The localized summary text.</param>
/// <param name="IsWarning">Whether the summary reports failures; otherwise it is a success.</param>
/// <param name="FolderPath">The folder the action opens, or <see langword="null"/> for no action.</param>
public sealed record InfoBarSummary(string Message, bool IsWarning, string? FolderPath);
