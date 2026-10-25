namespace JiuMi.Application.Errors;

/// <summary>
/// The surface that presents a registered error code.
/// </summary>
public enum ErrorCarrier
{
    /// <summary>The application-wide info bar in the main window.</summary>
    InfoBar,

    /// <summary>The standalone error window used for blocking failures.</summary>
    ErrorWindow,
}
