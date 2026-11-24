namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Bounded thread-safe in-memory LRU cache indexing silently scanned WeChat video metadata
/// by its 32-character <c>encfilekey</c> prefix. Items stay invisible to the UI until playback
/// correlation triggers them.
/// </summary>
public sealed class WeChatMetadataCache
{
    /// <summary>
    /// The default maximum number of entries retained in memory.
    /// </summary>
    public const int DefaultCapacity = 500;

    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _entries;
    private readonly LinkedList<CacheEntry> _lru = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatMetadataCache"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of entries to keep.</param>
    public WeChatMetadataCache(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);

        _capacity = capacity;
        _entries = new Dictionary<string, LinkedListNode<CacheEntry>>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the number of entries currently cached.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Inserts or updates a captured item indexed by its <c>encfilekey</c> prefix.
    /// Items without a valid <c>encfilekey</c> prefix are ignored.
    /// </summary>
    /// <param name="item">The silently scanned media metadata.</param>
    /// <returns><see langword="true"/> when the item was indexed.</returns>
    public bool Upsert(WeChatVideoCaptureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!WeChatVideoUrl.TryExtractEncFileKeyPrefix(item.Url, out string prefix))
        {
            return false;
        }

        Upsert(prefix, item);
        return true;
    }

    /// <summary>
    /// Inserts or updates a captured item under the specified <c>encfilekey</c> prefix.
    /// </summary>
    /// <param name="encFileKeyPrefix">The 32-character <c>encfilekey</c> prefix.</param>
    /// <param name="item">The media metadata.</param>
    public void Upsert(string encFileKeyPrefix, WeChatVideoCaptureItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encFileKeyPrefix);
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            if (_entries.TryGetValue(encFileKeyPrefix, out LinkedListNode<CacheEntry>? existing))
            {
                if (Score(item) >= Score(existing.Value.Item))
                {
                    existing.Value = new CacheEntry(encFileKeyPrefix, item);
                }

                _lru.Remove(existing);
                _lru.AddFirst(existing);
                return;
            }

            if (_entries.Count >= _capacity && _lru.Last is LinkedListNode<CacheEntry> oldest)
            {
                _lru.RemoveLast();
                _entries.Remove(oldest.Value.Prefix);
            }

            var node = new LinkedListNode<CacheEntry>(new CacheEntry(encFileKeyPrefix, item));
            _lru.AddFirst(node);
            _entries.Add(encFileKeyPrefix, node);
        }
    }

    /// <summary>
    /// Looks up cached metadata by <c>encfilekey</c> prefix and refreshes its recency.
    /// </summary>
    /// <param name="encFileKeyPrefix">The 32-character <c>encfilekey</c> prefix.</param>
    /// <param name="item">The cached metadata when found.</param>
    /// <returns><see langword="true"/> when an entry existed.</returns>
    public bool TryGet(string encFileKeyPrefix, out WeChatVideoCaptureItem? item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encFileKeyPrefix);

        lock (_gate)
        {
            if (_entries.TryGetValue(encFileKeyPrefix, out LinkedListNode<CacheEntry>? node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                item = node.Value.Item;
                return true;
            }
        }

        item = null;
        return false;
    }

    /// <summary>
    /// Removes all cached entries.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _lru.Clear();
        }
    }

    /// <summary>
    /// Scores metadata completeness so a fresher complete record replaces a partial one,
    /// while a partial record never clobbers a complete one.
    /// </summary>
    /// <param name="item">The candidate metadata item.</param>
    /// <returns>A numeric completeness score.</returns>
    public static int Score(WeChatVideoCaptureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        int score = 0;
        if (item.Url.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(item.DecodeKey))
        {
            score += 4;
        }

        if (item.Url.Contains("/20302/", StringComparison.Ordinal))
        {
            score += 2;
        }

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(item.Uploader))
        {
            score += 1;
        }

        if (item.Specs.Count > 0)
        {
            score += 1;
        }

        return score;
    }

    private sealed record CacheEntry(string Prefix, WeChatVideoCaptureItem Item);
}
