namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Correlates <c>NetworkService</c> playback stream keys against the silent
/// <see cref="WeChatMetadataCache"/>, suppressing pre-existing baseline videos and preload noise.
/// </summary>
public sealed class WeChatPlaybackCorrelator
{
    private const int MaxUnresolvedFeedRetries = 5;

    private readonly object _gate = new();
    private readonly WeChatMetadataCache _cache;
    private readonly HashSet<string> _seenStreamPrefixes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenPlayedFeedIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _feedRetryCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _feedToEncPrefix = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emittedPrefixes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emittedFeedIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPlaybackCorrelator"/> class.
    /// </summary>
    /// <param name="cache">The silent metadata cache populated by Phase 1.</param>
    public WeChatPlaybackCorrelator(WeChatMetadataCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    /// <summary>
    /// Records the initial <c>NetworkService</c> snapshot taken when listening starts,
    /// so videos played before listening started are ignored.
    /// </summary>
    /// <param name="encFileKeyPrefixes">The <c>encfilekey</c> prefixes present at start.</param>
    public void SetBaseline(IEnumerable<string> encFileKeyPrefixes)
    {
        ArgumentNullException.ThrowIfNull(encFileKeyPrefixes);

        lock (_gate)
        {
            foreach (string prefix in encFileKeyPrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    _seenStreamPrefixes.Add(prefix);
                }
            }
        }
    }

    /// <summary>
    /// Filters a <c>NetworkService</c> scan snapshot down to <c>encfilekey</c> prefixes
    /// that have not been seen in the baseline or any previous poll.
    /// </summary>
    /// <param name="currentPrefixes">The prefixes found in the latest scan.</param>
    /// <returns>The newly observed <c>encfilekey</c> prefixes.</returns>
    public IReadOnlyList<string> FilterNewEncFileKeys(IEnumerable<string> currentPrefixes)
    {
        ArgumentNullException.ThrowIfNull(currentPrefixes);

        var newlyObserved = new List<string>();
        lock (_gate)
        {
            foreach (string prefix in currentPrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) && _seenStreamPrefixes.Add(prefix))
                {
                    newlyObserved.Add(prefix);
                }
            }
        }

        return newlyObserved;
    }

    /// <summary>
    /// Attempts to resolve a newly played <c>encfilekey</c> prefix from the silent metadata cache
    /// and marks it as emitted so the same video is not emitted twice.
    /// </summary>
    /// <param name="encFileKeyPrefix">The newly observed <c>encfilekey</c> prefix.</param>
    /// <param name="item">The resolved item when found and not previously emitted.</param>
    /// <returns><see langword="true"/> when the item should be emitted to the session.</returns>
    public bool TryResolveFromCache(string encFileKeyPrefix, out WeChatVideoCaptureItem? item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encFileKeyPrefix);

        if (!_cache.TryGet(encFileKeyPrefix, out WeChatVideoCaptureItem? cached) || cached is null)
        {
            item = null;
            return false;
        }

        lock (_gate)
        {
            if (!_emittedPrefixes.Add(encFileKeyPrefix))
            {
                item = null;
                return false;
            }
        }

        item = cached;
        return true;
    }

    /// <summary>
    /// Accepts a metadata item found via targeted fallback scanning, caches it, and marks its
    /// <c>encfilekey</c> prefix as emitted.
    /// </summary>
    /// <param name="encFileKeyPrefix">The played <c>encfilekey</c> prefix.</param>
    /// <param name="item">The metadata resolved by targeted lookup.</param>
    /// <returns><see langword="true"/> when this prefix has not been emitted yet.</returns>
    public bool TryAcceptResolved(string encFileKeyPrefix, WeChatVideoCaptureItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encFileKeyPrefix);
        ArgumentNullException.ThrowIfNull(item);

        _cache.Upsert(encFileKeyPrefix, item);

        lock (_gate)
        {
            return _emittedPrefixes.Add(encFileKeyPrefix);
        }
    }

    /// <summary>
    /// Records the initial <c>videoPlayStart</c> events present when listening starts,
    /// so previously played videos are ignored until played again with a new <c>play_id</c>.
    /// </summary>
    /// <param name="events">The playback start events already present at start.</param>
    public void SetPlaybackBaseline(IEnumerable<WeChatPlaybackStartEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            foreach (WeChatPlaybackStartEvent playEvent in events)
            {
                if (!string.IsNullOrWhiteSpace(playEvent.FeedId))
                {
                    _seenPlayedFeedIds.Add(playEvent.EventKey);
                }
            }
        }
    }

    /// <summary>
    /// Filters a <c>videoPlayStart</c> scan down to events that have not been seen in the
    /// baseline or any previous poll.
    /// </summary>
    /// <param name="currentEvents">The playback start events found in the latest scan.</param>
    /// <returns>The newly observed playback start events.</returns>
    public IReadOnlyList<WeChatPlaybackStartEvent> FilterNewPlaybackStarts(
        IEnumerable<WeChatPlaybackStartEvent> currentEvents)
    {
        ArgumentNullException.ThrowIfNull(currentEvents);

        var newlyPlayed = new List<WeChatPlaybackStartEvent>();
        lock (_gate)
        {
            foreach (WeChatPlaybackStartEvent playEvent in currentEvents)
            {
                if (!string.IsNullOrWhiteSpace(playEvent.FeedId) &&
                    _seenPlayedFeedIds.Add(playEvent.EventKey))
                {
                    newlyPlayed.Add(playEvent);
                }
            }
        }

        return newlyPlayed;
    }

    /// <summary>
    /// Records the initial <c>videoPlayStart</c> feed IDs present when listening starts,
    /// so previously played videos are ignored.
    /// </summary>
    /// <param name="feedIds">The feed identifiers already present at start.</param>
    public void SetPlayedFeedBaseline(IEnumerable<string> feedIds)
    {
        ArgumentNullException.ThrowIfNull(feedIds);
        SetPlaybackBaseline(feedIds.Select(id => new WeChatPlaybackStartEvent(id, PlayId: null)));
    }

    /// <summary>
    /// Filters a <c>videoPlayStart</c> scan down to feed IDs that have not been seen in the
    /// baseline or any previous poll.
    /// </summary>
    /// <param name="currentFeedIds">The feed identifiers found in the latest scan.</param>
    /// <returns>The newly played feed identifiers.</returns>
    public IReadOnlyList<string> FilterNewPlayedFeedIds(IEnumerable<string> currentFeedIds)
    {
        ArgumentNullException.ThrowIfNull(currentFeedIds);
        return FilterNewPlaybackStarts(currentFeedIds.Select(id => new WeChatPlaybackStartEvent(id, PlayId: null)))
            .Select(e => e.FeedId)
            .ToArray();
    }

    /// <summary>
    /// Records a mapping from a numeric <c>feed_id</c> to its 32-character <c>encfilekey</c> prefix.
    /// Bounded to avoid unbounded growth over long sessions.
    /// </summary>
    /// <param name="feedId">The numeric feed identifier.</param>
    /// <param name="encFileKeyPrefix">The 32-character <c>encfilekey</c> prefix.</param>
    public void RecordFeedMapping(string feedId, string encFileKeyPrefix)
    {
        if (string.IsNullOrWhiteSpace(feedId) || string.IsNullOrWhiteSpace(encFileKeyPrefix))
        {
            return;
        }

        lock (_gate)
        {
            if (_feedToEncPrefix.Count >= 2000 && !_feedToEncPrefix.ContainsKey(feedId))
            {
                _feedToEncPrefix.Clear();
            }

            _feedToEncPrefix[feedId] = encFileKeyPrefix;
        }
    }

    /// <summary>
    /// Looks up the 32-character <c>encfilekey</c> prefix previously mapped to <paramref name="feedId"/>.
    /// </summary>
    /// <param name="feedId">The numeric feed identifier.</param>
    /// <param name="encFileKeyPrefix">The mapped <c>encfilekey</c> prefix when found.</param>
    /// <returns><see langword="true"/> when a mapping exists.</returns>
    public bool TryGetEncFileKeyPrefix(string feedId, out string? encFileKeyPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedId);

        lock (_gate)
        {
            return _feedToEncPrefix.TryGetValue(feedId, out encFileKeyPrefix);
        }
    }

    /// <summary>
    /// Looks up cached metadata by <c>encfilekey</c> prefix for a specific playback event without
    /// permanently blocking future replays of the same <c>encfilekey</c> with a new <c>play_id</c>.
    /// </summary>
    /// <param name="eventKey">The unique playback event key.</param>
    /// <param name="encFileKeyPrefix">The 32-character <c>encfilekey</c> prefix.</param>
    /// <param name="item">The cached metadata when found.</param>
    /// <returns><see langword="true"/> when this event has not been emitted yet and metadata exists.</returns>
    public bool TryResolveForEvent(
        string eventKey,
        string encFileKeyPrefix,
        out WeChatVideoCaptureItem? item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(encFileKeyPrefix);

        if (!_cache.TryGet(encFileKeyPrefix, out WeChatVideoCaptureItem? cached) || cached is null)
        {
            item = null;
            return false;
        }

        lock (_gate)
        {
            if (!_emittedFeedIds.Add(eventKey))
            {
                item = null;
                return false;
            }
        }

        item = cached;
        return true;
    }

    /// <summary>
    /// Caches metadata resolved by targeted lookup and marks <paramref name="eventKey"/> as emitted,
    /// allowing future replays with a new <c>play_id</c> to refresh expired or previously failed items.
    /// </summary>
    /// <param name="eventKey">The unique playback event key.</param>
    /// <param name="encFileKeyPrefix">The 32-character <c>encfilekey</c> prefix.</param>
    /// <param name="item">The resolved metadata.</param>
    /// <returns><see langword="true"/> when this event has not been emitted yet.</returns>
    public bool TryAcceptForEvent(
        string eventKey,
        string encFileKeyPrefix,
        WeChatVideoCaptureItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(encFileKeyPrefix);
        ArgumentNullException.ThrowIfNull(item);

        _cache.Upsert(encFileKeyPrefix, item);

        lock (_gate)
        {
            return _emittedFeedIds.Add(eventKey);
        }
    }

    /// <summary>
    /// Marks a played <paramref name="feedId"/> as emitted when not already emitted.
    /// </summary>
    /// <param name="feedId">The numeric feed identifier.</param>
    /// <returns><see langword="true"/> if this feed has not been emitted yet.</returns>
    public bool TryMarkFeedEmitted(string feedId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedId);

        lock (_gate)
        {
            return _emittedFeedIds.Add(feedId);
        }
    }

    /// <summary>
    /// Allows an unresolved playback event to be retried on subsequent polls up to a
    /// bounded attempt limit, covering cases where metadata or mapping arrives shortly after
    /// <c>videoPlayStart</c>.
    /// </summary>
    /// <param name="playEvent">The unresolved playback start event.</param>
    public void ReleaseUnresolvedEvent(WeChatPlaybackStartEvent playEvent)
    {
        ArgumentNullException.ThrowIfNull(playEvent);

        lock (_gate)
        {
            if (_emittedFeedIds.Contains(playEvent.EventKey))
            {
                return;
            }

            int attempts = _feedRetryCounts.GetValueOrDefault(playEvent.EventKey) + 1;
            _feedRetryCounts[playEvent.EventKey] = attempts;
            if (attempts < MaxUnresolvedFeedRetries)
            {
                _seenPlayedFeedIds.Remove(playEvent.EventKey);
            }
        }
    }

    /// <summary>
    /// Clears baseline and emission history for a fresh listening session.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _seenStreamPrefixes.Clear();
            _seenPlayedFeedIds.Clear();
            _feedRetryCounts.Clear();
            _feedToEncPrefix.Clear();
            _emittedPrefixes.Clear();
            _emittedFeedIds.Clear();
        }
    }
}
