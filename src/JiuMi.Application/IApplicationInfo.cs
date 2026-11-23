using JiuMi.Application.Updates;

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

    /// <summary>
    /// Gets the absolute URL of the public GitHub source repository.
    /// </summary>
    string RepositoryUrl
    {
        get;
    }

    /// <summary>
    /// Gets the absolute URL of the public Gitee mirror repository.
    /// </summary>
    string GiteeRepositoryUrl
    {
        get;
    }

    /// <summary>
    /// Gets the ordered release manifest sources checked for application updates (Gitee first, then GitHub).
    /// </summary>
    IReadOnlyList<AppUpdateSource> UpdateSources
    {
        get;
    }
}
