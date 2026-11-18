namespace JiuMi.Application.Errors;

/// <summary>
/// Holds the retry actions the info bar runs for registered error codes that can be retried in place.
/// </summary>
public sealed class ErrorRetryRegistry
{
    private readonly Dictionary<string, Func<Task<bool>>> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers the retry action for an event code, replacing an earlier action for the same code.
    /// </summary>
    /// <param name="code">The stable event code.</param>
    /// <param name="retry">
    /// Reruns the failed operation with the current in-memory state; returns <see langword="true"/>
    /// when the notice can close.
    /// </param>
    public void Register(string code, Func<Task<bool>> retry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(retry);

        lock (_handlers)
        {
            _handlers[code] = retry;
        }
    }

    /// <summary>
    /// Removes the retry action for an event code, when one is registered.
    /// </summary>
    /// <param name="code">The stable event code.</param>
    public void Unregister(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        lock (_handlers)
        {
            _handlers.Remove(code);
        }
    }

    /// <summary>
    /// Finds the retry action for an event code.
    /// </summary>
    /// <param name="code">The stable event code.</param>
    /// <returns>The retry action, or <see langword="null"/> when none is registered.</returns>
    public Func<Task<bool>>? Find(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        lock (_handlers)
        {
            return _handlers.TryGetValue(code, out Func<Task<bool>>? retry) ? retry : null;
        }
    }
}
