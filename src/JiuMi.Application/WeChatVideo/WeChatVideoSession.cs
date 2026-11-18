namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Holds the videos captured since the application started, de-duplicated by the feed identifiers
/// and ordered newest first. Nothing is persisted.
/// </summary>
public sealed class WeChatVideoSession
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WeChatVideoItem> _byKey = new(StringComparer.Ordinal);
    private readonly List<WeChatVideoItem> _ordered = [];
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoSession"/> class.
    /// </summary>
    /// <param name="timeProvider">Supplies the local capture time.</param>
    public WeChatVideoSession(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Occurs after an item was added, refreshed or removed. Raised on the thread that made the
    /// change, outside the session lock.
    /// </summary>
    public event EventHandler<WeChatVideoSessionChangedEventArgs>? Changed;

    /// <summary>
    /// Gets a snapshot of the items, newest first.
    /// </summary>
    public IReadOnlyList<WeChatVideoItem> Items
    {
        get
        {
            lock (_gate)
            {
                return _ordered.ToArray();
            }
        }
    }

    /// <summary>
    /// Adds a new video or refreshes the capture of the video already recognised by the same key.
    /// A refresh keeps the first capture time and the download state.
    /// </summary>
    /// <param name="capture">The item the injected page reported.</param>
    /// <returns>The item after the change.</returns>
    public WeChatVideoItem AddOrUpdate(WeChatVideoCaptureItem capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        string key = WeChatVideoUrl.CreateDeduplicationKey(capture);
        WeChatVideoItem item;
        WeChatVideoSessionChange change;
        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out WeChatVideoItem? existing))
            {
                item = existing with
                {
                    Capture = capture
                };
                Replace(key, item);
                change = WeChatVideoSessionChange.Updated;
            }
            else
            {
                item = new WeChatVideoItem
                {
                    Key = key,
                    Capture = capture,
                    CapturedAt = _timeProvider.GetLocalNow(),
                };
                _byKey.Add(key, item);
                _ordered.Insert(0, item);
                change = WeChatVideoSessionChange.Added;
            }
        }

        RaiseChanged(change, item);
        return item;
    }

    /// <summary>
    /// Removes a video from the list.
    /// </summary>
    /// <param name="key">The de-duplication key.</param>
    /// <returns><see langword="true"/> when an item was removed.</returns>
    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        WeChatVideoItem? removed = null;
        lock (_gate)
        {
            if (_byKey.Remove(key, out WeChatVideoItem? item))
            {
                _ordered.RemoveAll(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
                removed = item;
            }
        }

        if (removed is null)
        {
            return false;
        }

        RaiseChanged(WeChatVideoSessionChange.Removed, removed);
        return true;
    }

    /// <summary>
    /// Replaces an item with the result of <paramref name="update"/>.
    /// </summary>
    /// <param name="key">The de-duplication key.</param>
    /// <param name="update">Produces the new item from the current one.</param>
    /// <param name="updated">The new item when the key was found.</param>
    /// <returns><see langword="true"/> when the key was found.</returns>
    public bool TryUpdate(
        string key,
        Func<WeChatVideoItem, WeChatVideoItem> update,
        out WeChatVideoItem? updated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(update);

        updated = null;
        lock (_gate)
        {
            if (!_byKey.TryGetValue(key, out WeChatVideoItem? existing))
            {
                return false;
            }

            updated = update(existing) with
            {
                Key = key,
                CapturedAt = existing.CapturedAt
            };
            Replace(key, updated);
        }

        RaiseChanged(WeChatVideoSessionChange.Updated, updated);
        return true;
    }

    private void Replace(string key, WeChatVideoItem item)
    {
        _byKey[key] = item;
        int index = _ordered.FindIndex(
            candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
        _ordered[index] = item;
    }

    private void RaiseChanged(WeChatVideoSessionChange change, WeChatVideoItem item)
    {
        Changed?.Invoke(this, new WeChatVideoSessionChangedEventArgs(change, item));
    }
}
