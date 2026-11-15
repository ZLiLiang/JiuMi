using System.Runtime.Versioning;
using Microsoft.Win32;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Reads and writes the current user's WinINet internet settings.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class RegistryWinInetRegistry : IWinInetRegistry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    /// <inheritdoc />
    public int? ReadInt32(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) as int?;
    }

    /// <inheritdoc />
    public string? ReadString(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) as string;
    }

    /// <inheritdoc />
    public void WriteInt32(string name, int value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    /// <inheritdoc />
    public void WriteString(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    /// <inheritdoc />
    public void Delete(string name)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.DeleteValue(name, throwOnMissingValue: false);
    }
}
