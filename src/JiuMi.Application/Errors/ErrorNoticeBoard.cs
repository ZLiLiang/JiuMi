namespace JiuMi.Application.Errors;

/// <summary>
/// Holds the info bar notices waiting to be shown, one per event code.
/// </summary>
public sealed class ErrorNoticeBoard
{
    private readonly HashSet<string> _announcedCodes = new(StringComparer.Ordinal);
    private readonly List<ErrorNotice> _pending = [];
    private ErrorNotice? _deferredPersistent;

    /// <summary>
    /// Gets the notice currently shown, if any.
    /// </summary>
    public ErrorNotice? Current
    {
        get;
        private set;
    }

    /// <summary>
    /// Offers a record for presentation; repeats of an announced code are ignored. A persistent
    /// notice steps aside for the new notice and returns once the queue drains.
    /// </summary>
    /// <param name="record">The reported failure.</param>
    /// <returns><see langword="true"/> when a new notice was queued.</returns>
    public bool Offer(ErrorRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        ErrorPresentation? presentation = ErrorPresentationCatalog.Find(record.Code);
        if (presentation is null ||
            presentation.Carrier != ErrorCarrier.InfoBar ||
            !_announcedCodes.Add(record.Code))
        {
            return false;
        }

        var notice = new ErrorNotice(record.Code, presentation.Level, presentation.Action, presentation.IsPersistent);
        if (Current is { IsPersistent: true } persistent)
        {
            _deferredPersistent = persistent;
            Current = null;
        }

        Insert(notice);
        return true;
    }

    /// <summary>
    /// Makes the highest-priority queued notice current when nothing is shown, restoring a
    /// deferred persistent notice after the queue drains.
    /// </summary>
    /// <returns><see langword="true"/> when a queued notice became current.</returns>
    public bool ShowNext()
    {
        if (Current is not null)
        {
            return false;
        }

        if (_pending.Count > 0)
        {
            Current = _pending[0];
            _pending.RemoveAt(0);
            return true;
        }

        if (_deferredPersistent is not null)
        {
            Current = _deferredPersistent;
            _deferredPersistent = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the current notice unless it is persistent.
    /// </summary>
    /// <returns><see langword="true"/> when the current notice was cleared.</returns>
    public bool DismissCurrent()
    {
        if (Current is null || Current.IsPersistent)
        {
            return false;
        }

        Current = null;
        return true;
    }

    private void Insert(ErrorNotice notice)
    {
        int index = _pending.Count;
        for (int candidate = 0; candidate < _pending.Count; candidate++)
        {
            if (_pending[candidate].Level < notice.Level)
            {
                index = candidate;
                break;
            }
        }

        _pending.Insert(index, notice);
    }
}
