namespace JiuMi.Application.Errors;

/// <summary>
/// How a registered error code is presented to the user.
/// </summary>
/// <param name="Carrier">The surface that shows the error.</param>
/// <param name="Action">The single primary action the surface may offer.</param>
/// <param name="IsPersistent">Whether the notice stays until its condition clears.</param>
public sealed record ErrorPresentation(
    ErrorCarrier Carrier,
    ErrorNoticeAction Action = ErrorNoticeAction.None,
    bool IsPersistent = false);
