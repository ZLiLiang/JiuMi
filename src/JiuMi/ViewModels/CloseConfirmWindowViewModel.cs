using System;

namespace JiuMi.ViewModels;

/// <summary>
/// The text and checkbox state of the close confirmation dialog.
/// </summary>
public sealed class CloseConfirmWindowViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloseConfirmWindowViewModel"/> class.
    /// </summary>
    /// <param name="appName">The application name shown in the title bar.</param>
    /// <param name="heading">The question shown as the headline.</param>
    /// <param name="body">The explanation of what each action does.</param>
    /// <param name="dontAsk">The label of the remember-this-choice checkbox.</param>
    /// <param name="cancel">The cancel button text.</param>
    /// <param name="exit">The exit button text.</param>
    /// <param name="hideToTray">The hide-to-tray button text.</param>
    public CloseConfirmWindowViewModel(
        string appName,
        string heading,
        string body,
        string dontAsk,
        string cancel,
        string exit,
        string hideToTray)
    {
        ArgumentNullException.ThrowIfNull(appName);
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(dontAsk);
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(hideToTray);

        AppName = appName;
        Heading = heading;
        Body = body;
        DontAskText = dontAsk;
        CancelText = cancel;
        ExitText = exit;
        HideToTrayText = hideToTray;
    }

    /// <summary>
    /// Gets the application name shown in the title bar.
    /// </summary>
    public string AppName
    {
        get;
    }

    /// <summary>
    /// Gets the question shown as the headline.
    /// </summary>
    public string Heading
    {
        get;
    }

    /// <summary>
    /// Gets the explanation of what each action does.
    /// </summary>
    public string Body
    {
        get;
    }

    /// <summary>
    /// Gets the label of the remember-this-choice checkbox.
    /// </summary>
    public string DontAskText
    {
        get;
    }

    /// <summary>
    /// Gets the cancel button text.
    /// </summary>
    public string CancelText
    {
        get;
    }

    /// <summary>
    /// Gets the exit button text.
    /// </summary>
    public string ExitText
    {
        get;
    }

    /// <summary>
    /// Gets the hide-to-tray button text.
    /// </summary>
    public string HideToTrayText
    {
        get;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the chosen action should be remembered.
    /// </summary>
    public bool DontAsk
    {
        get;
        set;
    }
}
