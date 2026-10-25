using Avalonia.Threading;

namespace JiuMi.Threading;

/// <summary>
/// Queues work on the Avalonia UI thread.
/// </summary>
public sealed class AvaloniaUiThreadDispatcher : IUiThreadDispatcher
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
    }
}
