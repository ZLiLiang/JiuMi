using JiuMi.Application.Shell;

namespace JiuMi.Infrastructure.Shell;

/// <summary>
/// Stands in for the notification-area entry where the platform has none; the window keeps its
/// taskbar entry instead of hiding to a tray that does not exist.
/// </summary>
internal sealed class NullTrayIcon : ITrayIcon
{
    /// <inheritdoc />
    public event EventHandler? ShowRequested
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc />
    public event EventHandler? SettingsRequested
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc />
    public event EventHandler? ExitRequested
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc />
    public bool Show(string toolTip, TrayMenuText menu) => false;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
