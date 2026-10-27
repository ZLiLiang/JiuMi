namespace JiuMi.Application.Errors;

/// <summary>
/// The exit codes the shell reports when the process ends.
/// </summary>
public static class ExitCodes
{
    /// <summary>The process ended normally, including a launch that handed its request to the running instance.</summary>
    public const int Success = 0;

    /// <summary>A fatal failure ended the process.</summary>
    public const int Failure = 1;
}
