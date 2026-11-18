using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JiuMi.Application;
using JiuMi.Application.Localization;
using JiuMi.ViewModels;
using JiuMi.Views;

namespace JiuMi.WeChatVideo;

/// <summary>
/// Shows the borderless certificate confirmation over the shell's main window.
/// </summary>
public sealed class WeChatVideoCertificatePrompt : IWeChatVideoCertificatePrompt
{
    private readonly IApplicationInfo _applicationInfo;
    private readonly ILocalizationService _localization;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoCertificatePrompt"/> class.
    /// </summary>
    /// <param name="applicationInfo">Supplies the window title.</param>
    /// <param name="localization">Supplies the confirmation text.</param>
    /// <param name="logger">Receives prompts that cannot be shown.</param>
    public WeChatVideoCertificatePrompt(
        IApplicationInfo applicationInfo,
        ILocalizationService localization,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationInfo = applicationInfo;
        _localization = localization;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> ConfirmInstallAsync()
    {
        var viewModel = new WeChatConfirmWindowViewModel(
            _applicationInfo.Name,
            _localization["Tool.WeChatVideo.Certificate.Title"],
            _localization["Tool.WeChatVideo.Certificate.Body"],
            _localization["Tool.WeChatVideo.Certificate.Note"],
            _localization["Tool.WeChatVideo.Certificate.Cancel"],
            _localization["Tool.WeChatVideo.Certificate.Confirm"],
            isDanger: false);
        return ShowAsync(viewModel);
    }

    /// <inheritdoc />
    public Task<bool> ConfirmUninstallAsync()
    {
        var viewModel = new WeChatConfirmWindowViewModel(
            _applicationInfo.Name,
            _localization["Settings.Data.Certificate.Confirm.Title"],
            _localization["Settings.Data.Certificate.Confirm.Body"],
            note: null,
            _localization["Settings.Data.Certificate.Confirm.Cancel"],
            _localization["Settings.Data.Certificate.Uninstall"],
            isDanger: true);
        return ShowAsync(viewModel);
    }

    private async Task<bool> ShowAsync(WeChatConfirmWindowViewModel viewModel)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not
            IClassicDesktopStyleApplicationLifetime { MainWindow: { IsLoaded: true } owner })
        {
            _logger.Warning("A WeChat certificate prompt was requested without a usable main window");
            return false;
        }

#pragma warning disable CA1031 // The window is the UI boundary; a failed prompt must leave the certificate untouched.
        try
        {
            var window = new WeChatConfirmWindow
            {
                DataContext = viewModel,
            };
            return await window.ShowDialog<bool>(owner);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "The WeChat certificate prompt failed");
            return false;
        }
#pragma warning restore CA1031
    }
}
