using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Switches the current user's HTTPS proxy for one capture and restores the exact prior values.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WinInetSystemProxy
{
    private readonly IWinInetRegistry _registry;
    private readonly Action _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinInetSystemProxy"/> class backed by the real registry.
    /// </summary>
    public WinInetSystemProxy()
        : this(new RegistryWinInetRegistry(), RefreshWinInet)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinInetSystemProxy"/> class with test seams.
    /// </summary>
    /// <param name="registry">The registry values to read and write.</param>
    /// <param name="refresh">Notifies WinINet that the values changed.</param>
    internal WinInetSystemProxy(IWinInetRegistry registry, Action refresh)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(refresh);
        _registry = registry;
        _refresh = refresh;
    }

    /// <summary>
    /// Reads the five values that must be restored later.
    /// </summary>
    /// <returns>The current proxy configuration.</returns>
    public WinInetProxySnapshot CaptureSnapshot()
    {
        return new WinInetProxySnapshot(
            _registry.ReadInt32("ProxyEnable"),
            _registry.ReadString("ProxyServer"),
            _registry.ReadString("ProxyOverride"),
            _registry.ReadString("AutoConfigURL"),
            _registry.ReadInt32("AutoDetect"));
    }

    /// <summary>
    /// Points HTTPS at the local capture proxy, leaving the other protocols and the bypass list alone.
    /// </summary>
    /// <param name="port">The local capture proxy port.</param>
    public void ApplyHttpsProxy(int port)
    {
        string? existing = _registry.ReadString("ProxyServer");
        _registry.WriteString("ProxyServer", WinInetProxyServerValue.ForHttps(existing, port));
        _registry.WriteInt32("ProxyEnable", 1);

        // A PAC script or auto-detection would bypass the fixed local proxy.
        _registry.Delete("AutoConfigURL");
        _registry.WriteInt32("AutoDetect", 0);
        _refresh();
    }

    /// <summary>
    /// Restores every value captured by <see cref="CaptureSnapshot"/>, including absent values.
    /// </summary>
    /// <param name="snapshot">The values to restore.</param>
    public void Restore(WinInetProxySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        RestoreInt32("ProxyEnable", snapshot.ProxyEnable);
        RestoreString("ProxyServer", snapshot.ProxyServer);
        RestoreString("ProxyOverride", snapshot.ProxyOverride);
        RestoreString("AutoConfigURL", snapshot.AutoConfigUrl);
        RestoreInt32("AutoDetect", snapshot.AutoDetect);
        _refresh();
    }

    private void RestoreInt32(string name, int? value)
    {
        if (value is null)
        {
            _registry.Delete(name);
        }
        else
        {
            _registry.WriteInt32(name, value.Value);
        }
    }

    private void RestoreString(string name, string? value)
    {
        if (value is null)
        {
            _registry.Delete(name);
        }
        else
        {
            _registry.WriteString(name, value);
        }
    }

    private static void RefreshWinInet()
    {
        _ = InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
        _ = InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(
        IntPtr hInternet,
        int option,
        IntPtr buffer,
        int bufferLength);
}
