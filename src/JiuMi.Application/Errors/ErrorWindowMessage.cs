namespace JiuMi.Application.Errors;

/// <summary>
/// The text the error window shows for one failure.
/// </summary>
/// <param name="Headline">The short statement of what went wrong.</param>
/// <param name="Body">The explanation of what the user can do.</param>
public sealed record ErrorWindowMessage(string Headline, string Body);
