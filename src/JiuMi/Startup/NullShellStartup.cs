using Avalonia.Controls.ApplicationLifetimes;

namespace JiuMi.Startup;

/// <summary>
/// Stands in for <see cref="IShellStartup"/> when the application is built without a real startup,
/// such as by the XAML designer.
/// </summary>
internal sealed class NullShellStartup : IShellStartup
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static NullShellStartup Instance
    {
        get;
    } = new();

    private NullShellStartup()
    {
    }

    /// <inheritdoc />
    public void Start(IClassicDesktopStyleApplicationLifetime desktop) => ArgumentNullException.ThrowIfNull(desktop);
}
