namespace JiuMi.ViewModels;

/// <summary>
/// Describes a page change that the shell view may animate before the page is swapped.
/// </summary>
public sealed class PageNavigationRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageNavigationRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="page">The page the request targets.</param>
    /// <param name="fromCard">Whether a home tool card started the request.</param>
    public PageNavigationRequestedEventArgs(ShellPage page, bool fromCard)
    {
        Page = page;
        FromCard = fromCard;
    }

    /// <summary>
    /// Gets the page the request targets.
    /// </summary>
    public ShellPage Page
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether a home tool card started the request.
    /// </summary>
    public bool FromCard
    {
        get;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the view will play the transition and swap the page itself.
    /// </summary>
    public bool Handled
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the view accepts the request; a card activation is refused while another card animation plays.
    /// </summary>
    public bool Accepted
    {
        get;
        set;
    } = true;
}
