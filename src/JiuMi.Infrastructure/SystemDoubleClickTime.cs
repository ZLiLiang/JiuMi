using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JiuMi.Application;

namespace JiuMi.Infrastructure;

/// <summary>
/// Reads the operating system's double-click interval, with the Windows default as the fallback.
/// </summary>
public sealed class SystemDoubleClickTime : ISystemDoubleClickTime
{
    private const uint FallbackMilliseconds = 500;

    private readonly Func<uint?> _readDoubleClickTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemDoubleClickTime"/> class.
    /// </summary>
    public SystemDoubleClickTime()
        : this(static () => OperatingSystem.IsWindows() ? ReadDoubleClickTime() : null)
    {
    }

    internal SystemDoubleClickTime(Func<uint?> readDoubleClickTime)
    {
        ArgumentNullException.ThrowIfNull(readDoubleClickTime);
        _readDoubleClickTime = readDoubleClickTime;
    }

    /// <inheritdoc />
    public TimeSpan DoubleClickInterval
    {
        get
        {
            uint? milliseconds = _readDoubleClickTime();
            return TimeSpan.FromMilliseconds(
                milliseconds is > 0u ? milliseconds.Value : FallbackMilliseconds);
        }
    }

    [SupportedOSPlatform("windows")]
    private static uint ReadDoubleClickTime() => GetDoubleClickTime();

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDoubleClickTime();
}
