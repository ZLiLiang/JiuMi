namespace JiuMi.Application.Errors;

/// <summary>
/// The single primary action an info bar notice may offer.
/// </summary>
public enum ErrorNoticeAction
{
    /// <summary>The notice has no action button.</summary>
    None,

    /// <summary>Runs the failed operation again with the current in-memory state.</summary>
    Retry,

    /// <summary>Opens the effective settings folder.</summary>
    OpenFolder,
}
