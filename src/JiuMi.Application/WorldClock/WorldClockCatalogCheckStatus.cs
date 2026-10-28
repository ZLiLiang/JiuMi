namespace JiuMi.Application.WorldClock;

/// <summary>
/// The outcome of one catalog check.
/// </summary>
public enum WorldClockCatalogCheckStatus
{
    /// <summary>The data-and-connection switch is off; no request was made.</summary>
    Disabled,

    /// <summary>The check ran recently, or another check is already running.</summary>
    Skipped,

    /// <summary>The published version is the installed one.</summary>
    UpToDate,

    /// <summary>A newer version was downloaded, verified and cached.</summary>
    Updated,

    /// <summary>The check could not finish; the installed catalog stays in use.</summary>
    Failed,
}
