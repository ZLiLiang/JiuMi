using System.ComponentModel;
using JiuMi.Application.Logging;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Tracks whether the file sink is currently usable and tells the about section when it changes.
/// </summary>
internal sealed class LoggingStatus : ILoggingStatus
{
    private readonly Lock _gate = new();
    private bool _isFileLoggingAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingStatus"/> class.
    /// </summary>
    /// <param name="levelOverride">The raw level override, or <see langword="null"/> when unset.</param>
    public LoggingStatus(string? levelOverride)
    {
        LevelOverride = levelOverride;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public bool IsFileLoggingAvailable
    {
        get
        {
            lock (_gate)
            {
                return _isFileLoggingAvailable;
            }
        }
    }

    /// <inheritdoc />
    public string? LevelOverride
    {
        get;
    }

    internal void SetFileLoggingAvailable(bool isAvailable)
    {
        bool changed;
        lock (_gate)
        {
            changed = _isFileLoggingAvailable != isAvailable;
            _isFileLoggingAvailable = isAvailable;
        }

        if (changed)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFileLoggingAvailable)));
        }
    }
}
