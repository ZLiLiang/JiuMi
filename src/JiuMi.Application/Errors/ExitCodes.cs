namespace JiuMi.Application.Errors;

/// <summary>
/// The exit codes the shell reports when the process ends.
/// </summary>
public static class ExitCodes
{
    /// <summary>The application closed normally.</summary>
    public const int Normal = 0;

    /// <summary>A fatal failure ended the process.</summary>
    public const int Failure = 1;
}
