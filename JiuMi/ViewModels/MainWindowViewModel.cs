using JiuMi.Application;

namespace JiuMi.ViewModels;

/// <summary>
/// View model for the shell window.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="applicationInfo">Application name and version shown by the shell.</param>
    public MainWindowViewModel(IApplicationInfo applicationInfo)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        AppName = applicationInfo.Name;
        AppVersion = applicationInfo.Version;
    }

    /// <summary>
    /// Gets the product name shown in the title bar.
    /// </summary>
    public string AppName
    {
        get;
    }

    /// <summary>
    /// Gets the semantic version shown on the settings page.
    /// </summary>
    public string AppVersion
    {
        get;
    }
}
