using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JiuMi.Application.MyJiuMi;

namespace JiuMi.Infrastructure.MyJiuMi;

/// <summary>
/// Reads the system pointer position in physical screen pixels via Win32 <c>GetCursorPos</c>,
/// requiring no global hooks or elevated permissions.
/// </summary>
public sealed class SystemCursorPositionProvider : IMyJiuMiCursorPositionProvider
{
    private readonly Func<MyJiuMiScreenPoint?> _readPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemCursorPositionProvider"/> class.
    /// </summary>
    public SystemCursorPositionProvider()
        : this(static () => OperatingSystem.IsWindows() ? ReadWindowsCursorPosition() : null)
    {
    }

    internal SystemCursorPositionProvider(Func<MyJiuMiScreenPoint?> readPosition)
    {
        ArgumentNullException.ThrowIfNull(readPosition);
        _readPosition = readPosition;
    }

    /// <inheritdoc />
    public bool TryGetPosition(out MyJiuMiScreenPoint position)
    {
        if (_readPosition() is { } current)
        {
            position = current;
            return true;
        }

        position = default;
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static MyJiuMiScreenPoint? ReadWindowsCursorPosition() =>
        GetCursorPos(out NativePoint point)
            ? new MyJiuMiScreenPoint(point.X, point.Y)
            : null;

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
