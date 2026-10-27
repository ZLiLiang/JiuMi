namespace JiuMi.Application.Notifications;

/// <summary>
/// Delivers a short system notification that is only used when no window is visible.
/// </summary>
/// <remarks>
/// The Windows implementation lives in the infrastructure layer; other platforms accept the call and
/// do nothing.
/// </remarks>
public interface IUserNotification
{
    /// <summary>
    /// Shows a short notification.
    /// </summary>
    /// <param name="title">The notification title, which is the application name.</param>
    /// <param name="message">The localized single sentence shown as the body.</param>
    /// <param name="activated">Invoked when the user clicks the notification; may be <see langword="null"/>.</param>
    void Show(string title, string message, Action? activated);
}
