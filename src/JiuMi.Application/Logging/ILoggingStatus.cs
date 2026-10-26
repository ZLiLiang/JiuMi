using System.ComponentModel;

namespace JiuMi.Application.Logging;

/// <summary>
/// Reports whether file logging is currently working, for the about section.
/// </summary>
public interface ILoggingStatus : INotifyPropertyChanged
{
    /// <summary>
    /// Gets a value indicating whether events can be written to a log file.
    /// </summary>
    bool IsFileLoggingAvailable
    {
        get;
    }

    /// <summary>
    /// Gets the raw level override, or <see langword="null"/> when the environment did not set one.
    /// </summary>
    string? LevelOverride
    {
        get;
    }
}
