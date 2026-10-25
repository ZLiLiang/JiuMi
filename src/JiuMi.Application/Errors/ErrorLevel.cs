namespace JiuMi.Application.Errors;

/// <summary>
/// How a reported failure is presented to the user.
/// </summary>
public enum ErrorLevel
{
    /// <summary>A diagnostic detail that is recorded but never presented.</summary>
    Debug,

    /// <summary>A non-blocking problem that auto-dismisses after being shown.</summary>
    Warning,

    /// <summary>A non-blocking problem that stays until the user closes it.</summary>
    Error,
}
