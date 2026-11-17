namespace JiuMi.ViewModels;

/// <summary>
/// The text of a WeChat video certificate confirmation.
/// </summary>
public sealed class WeChatConfirmWindowViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatConfirmWindowViewModel"/> class.
    /// </summary>
    /// <param name="appTitle">The window title, used for accessibility.</param>
    /// <param name="heading">The question shown as the headline.</param>
    /// <param name="body">The explanation.</param>
    /// <param name="note">The optional smaller note.</param>
    /// <param name="cancel">The cancel button text.</param>
    /// <param name="confirm">The confirm button text.</param>
    /// <param name="isDanger">Whether the confirm action uses the secondary danger style.</param>
    public WeChatConfirmWindowViewModel(
        string appTitle,
        string heading,
        string body,
        string? note,
        string cancel,
        string confirm,
        bool isDanger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(cancel);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirm);

        AppTitle = appTitle;
        Heading = heading;
        Body = body;
        Note = note;
        CancelText = cancel;
        ConfirmText = confirm;
        IsDanger = isDanger;
    }

    /// <summary>
    /// Gets the window title, used for accessibility.
    /// </summary>
    public string AppTitle
    {
        get;
    }

    /// <summary>
    /// Gets the headline.
    /// </summary>
    public string Heading
    {
        get;
    }

    /// <summary>
    /// Gets the explanation.
    /// </summary>
    public string Body
    {
        get;
    }

    /// <summary>
    /// Gets the optional smaller note.
    /// </summary>
    public string? Note
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether a note is present.
    /// </summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>
    /// Gets the cancel button text.
    /// </summary>
    public string CancelText
    {
        get;
    }

    /// <summary>
    /// Gets the confirm button text.
    /// </summary>
    public string ConfirmText
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the confirm action uses the secondary danger style.
    /// </summary>
    public bool IsDanger
    {
        get;
    }
}
