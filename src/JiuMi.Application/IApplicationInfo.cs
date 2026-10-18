namespace JiuMi.Application;

/// <summary>
/// Describes the running application for display in the shell.
/// </summary>
public interface IApplicationInfo
{
    /// <summary>
    /// Gets the product name shown in the window title bar.
    /// </summary>
    string Name
    {
        get;
    }

    /// <summary>
    /// Gets the semantic version without build metadata.
    /// </summary>
    string Version
    {
        get;
    }
}
