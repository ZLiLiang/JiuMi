namespace JiuMi.Application.Errors;

/// <summary>
/// The action the error window's default controls perform.
/// </summary>
public enum ErrorWindowAction
{
    /// <summary>Leave the process with the failure exit code.</summary>
    Exit,

    /// <summary>Close the window and keep the application running.</summary>
    Continue,
}
