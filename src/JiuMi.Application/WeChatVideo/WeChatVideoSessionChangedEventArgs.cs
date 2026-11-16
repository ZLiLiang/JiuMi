namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Describes one change to the session list.
/// </summary>
public sealed class WeChatVideoSessionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoSessionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="change">What happened.</param>
    /// <param name="item">The item after the change; the removed snapshot when removed.</param>
    public WeChatVideoSessionChangedEventArgs(WeChatVideoSessionChange change, WeChatVideoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Change = change;
        Item = item;
    }

    /// <summary>
    /// Gets what happened.
    /// </summary>
    public WeChatVideoSessionChange Change
    {
        get;
    }

    /// <summary>
    /// Gets the item after the change.
    /// </summary>
    public WeChatVideoItem Item
    {
        get;
    }
}
