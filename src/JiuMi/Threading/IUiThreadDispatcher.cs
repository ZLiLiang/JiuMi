namespace JiuMi.Threading;

/// <summary>
/// Queues work on the thread that owns the interface.
/// </summary>
public interface IUiThreadDispatcher
{
    /// <summary>
    /// Queues <paramref name="action"/> to run after the current input event finishes.
    /// </summary>
    /// <param name="action">The work to run on the interface thread.</param>
    void Post(Action action);
}
