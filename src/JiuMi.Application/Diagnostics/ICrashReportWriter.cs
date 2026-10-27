namespace JiuMi.Application.Diagnostics;

/// <summary>
/// Writes the plain-text file left behind when the process cannot continue.
/// </summary>
public interface ICrashReportWriter
{
    /// <summary>
    /// Writes a crash file into the effective log directory, best effort.
    /// </summary>
    /// <param name="operation">The stage or operation that was running.</param>
    /// <param name="exception">The exception that ended the process.</param>
    void Write(string operation, Exception exception);
}
