using Avalonia.Threading;
using JiuMi.Threading;

namespace JiuMi.Desktop;

internal sealed class AvaloniaUiThreadDispatcher : IUiThreadDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
    }
}
