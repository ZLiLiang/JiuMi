namespace JiuMi.Application.Errors;

/// <summary>
/// A queued info bar notice for one registered event code.
/// </summary>
/// <param name="Code">The stable event code.</param>
/// <param name="Level">The severity that controls closing and auto-dismissal.</param>
/// <param name="Action">The single primary action the notice offers.</param>
/// <param name="IsPersistent">Whether the notice cannot be closed.</param>
public sealed record ErrorNotice(
    string Code,
    ErrorLevel Level,
    ErrorNoticeAction Action,
    bool IsPersistent);
