using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JiuMi.Application;

namespace JiuMi.Infrastructure;

/// <summary>
/// Reads the operating system's "show animations" accessibility preference.
/// </summary>
public sealed class SystemAnimationSettings : ISystemAnimationSettings
{
    private const uint GetClientAreaAnimation = 0x1042;

    /// <inheritdoc />
    public bool AreClientAreaAnimationsEnabled =>
        !OperatingSystem.IsWindows() || ReadClientAreaAnimation();

    [SupportedOSPlatform("windows")]
    private static bool ReadClientAreaAnimation()
    {
        // A failed read keeps the default: animations stay on.
        return !SystemParametersInfo(GetClientAreaAnimation, 0, out bool enabled, 0) || enabled;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);
}
