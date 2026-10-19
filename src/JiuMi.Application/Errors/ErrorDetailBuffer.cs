namespace JiuMi.Application.Errors;

/// <summary>
/// Keeps the most recent error records and merges repeats of the same event code.
/// </summary>
public sealed class ErrorDetailBuffer
{
    /// <summary>
    /// The maximum number of distinct event codes kept for the session.
    /// </summary>
    public const int Capacity = 50;

    private readonly Lock _gate = new();
    private readonly List<ErrorRecord> _records = [];

    /// <summary>
    /// Gets a snapshot of the retained records, oldest first.
    /// </summary>
    public IReadOnlyList<ErrorRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToArray();
            }
        }
    }

    /// <summary>
    /// Adds a record, merging it into an existing record with the same event code.
    /// </summary>
    /// <param name="record">The record to add.</param>
    /// <returns>The record as stored, with the session count.</returns>
    public ErrorRecord Add(ErrorRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_gate)
        {
            int existingIndex = _records.FindIndex(
                candidate => string.Equals(candidate.Code, record.Code, StringComparison.Ordinal));

            ErrorRecord stored = existingIndex >= 0
                ? record with
                {
                    Count = _records[existingIndex].Count + 1,
                }
                : record;

            if (existingIndex >= 0)
            {
                _records.RemoveAt(existingIndex);
            }

            _records.Add(stored);
            if (_records.Count > Capacity)
            {
                _records.RemoveRange(0, _records.Count - Capacity);
            }

            return stored;
        }
    }

    /// <summary>
    /// Finds the retained record for an event code.
    /// </summary>
    /// <param name="code">The stable event code.</param>
    /// <returns>The record, or <see langword="null"/> when it is no longer retained.</returns>
    public ErrorRecord? Find(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        lock (_gate)
        {
            foreach (ErrorRecord record in _records)
            {
                if (string.Equals(record.Code, code, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }
    }
}
