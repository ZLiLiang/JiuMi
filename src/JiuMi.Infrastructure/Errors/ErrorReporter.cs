using JiuMi.Application.Errors;

namespace JiuMi.Infrastructure.Errors;

/// <summary>
/// Stores reported failures in memory and raises an in-process event for the UI.
/// </summary>
public sealed class ErrorReporter : IErrorReporter
{
    /// <inheritdoc />
    public ErrorDetailBuffer Details
    {
        get;
    } = new();

    /// <inheritdoc />
    public event EventHandler<ErrorRecord>? Reported;

    /// <inheritdoc />
    public void Report(ErrorRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        ErrorRecord stored = Details.Add(record);
        Reported?.Invoke(this, stored);
    }
}
