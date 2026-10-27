using JiuMi.Application.Notifications;

namespace JiuMi.Infrastructure.Notifications;

/// <summary>
/// Accepts user notifications on platforms without a system surface for them.
/// </summary>
internal sealed class NullUserNotification : IUserNotification
{
    /// <inheritdoc />
    public void Show(string title, string message, Action? activated)
    {
    }
}
