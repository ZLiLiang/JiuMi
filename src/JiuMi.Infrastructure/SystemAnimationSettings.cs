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

    private readonly Func<bool?> _readClientAreaAnimation;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAnimationSettings"/> class.
    /// </summary>
    public SystemAnimationSettings()
        : this(static () => OperatingSystem.IsWindows() ? ReadClientAreaAnimation() : null)
    {
    }

    internal SystemAnimationSettings(Func<bool?> readClientAreaAnimation)
    {
        ArgumentNullException.ThrowIfNull(readClientAreaAnimation);
        _readClientAreaAnimation = readClientAreaAnimation;
    }

    /// <inheritdoc />
    /// <remarks>A failed or unavailable read keeps animations on.</remarks>
    public bool AreClientAreaAnimationsEnabled => _readClientAreaAnimation() ?? true;

    [SupportedOSPlatform("windows")]
    private static bool? ReadClientAreaAnimation() =>
        SystemParametersInfo(GetClientAreaAnimation, 0, out bool enabled, 0) ? enabled : null;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);
}
