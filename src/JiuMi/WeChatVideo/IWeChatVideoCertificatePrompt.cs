namespace JiuMi.WeChatVideo;

/// <summary>
/// Asks the user to trust or remove the local certificate the WeChat video listener uses.
/// </summary>
public interface IWeChatVideoCertificatePrompt
{
    /// <summary>
    /// Shows the certificate installation confirmation.
    /// </summary>
    /// <returns><see langword="true"/> when the user allowed the installation.</returns>
    Task<bool> ConfirmInstallAsync();

    /// <summary>
    /// Shows the certificate removal confirmation.
    /// </summary>
    /// <returns><see langword="true"/> when the user allowed the removal.</returns>
    Task<bool> ConfirmUninstallAsync();
}
