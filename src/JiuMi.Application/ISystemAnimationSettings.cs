namespace JiuMi.Application;

/// <summary>
/// Reports the operating system's accessibility preference for client-area animations.
/// </summary>
public interface ISystemAnimationSettings
{
    /// <summary>
    /// Gets a value indicating whether animations requested by the application should be played.
    /// </summary>
    bool AreClientAreaAnimationsEnabled
    {
        get;
    }
}
