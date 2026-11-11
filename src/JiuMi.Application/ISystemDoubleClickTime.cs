namespace JiuMi.Application;

/// <summary>
/// Reports the operating system's double-click interval for the pet's click gestures.
/// </summary>
public interface ISystemDoubleClickTime
{
    /// <summary>
    /// Gets the longest interval between two clicks that still counts as a double click.
    /// </summary>
    TimeSpan DoubleClickInterval
    {
        get;
    }
}
