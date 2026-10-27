using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JiuMi.Application.Notifications;
using JiuMi.Application.Shell;

namespace JiuMi.Infrastructure.Shell;

/// <summary>
/// The Windows notification-area icon and its short notifications.
/// </summary>
/// <remarks>
/// The icon is a hidden top-level window on the calling thread, which must run a Win32 message loop;
/// the application creates it on the interface thread once the shell is visible. The window is a real
/// top-level window rather than a message-only one because the shell requires it to become the
/// foreground window before the native menu can dismiss itself. The tray image is the executable's own
/// icon, so the notification area, the taskbar and the window all read the same mark. Native failures
/// are logged and reported as "not available": a missing tray must never stop the shell.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class WindowsTrayIcon : ITrayIcon, IUserNotification
{
    private const uint CallbackMessage = 0x8000 + 1;
    private const uint IconId = 1;
    private const uint CommandShow = 1;
    private const uint CommandSettings = 2;
    private const uint CommandExit = 3;

    private const uint NotifyAdd = 0;
    private const uint NotifyModify = 1;
    private const uint NotifyDelete = 2;

    private const uint NotifyFlagMessage = 0x1;
    private const uint NotifyFlagIcon = 0x2;
    private const uint NotifyFlagTip = 0x4;
    private const uint NotifyFlagInfo = 0x10;
    private const uint NotifyInfoInfo = 0x1;

    private const uint MenuString = 0x0;
    private const uint TrackRightButton = 0x2;
    private const uint TrackNoNotify = 0x80;
    private const uint TrackReturnCommand = 0x100;

    private const int WmLeftButtonUp = 0x0202;
    private const int WmLeftButtonDoubleClick = 0x0203;
    private const int WmRightButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;
    private const int WmNull = 0x0000;
    private const int NinBalloonHide = 0x0403;
    private const int NinBalloonTimeout = 0x0404;
    private const int NinBalloonUserClick = 0x0405;
    private const int TipLength = 127;
    private const int InfoTitleLength = 63;
    private const int InfoLength = 255;
    private static readonly IntPtr _applicationIconId = new(32512);

    private readonly Serilog.ILogger _logger;
    private readonly WindowProcedure _windowProcedure;
    private readonly string _className;

    private IntPtr _window;
    private IntPtr _icon;
    private bool _ownsIcon;
    private bool _visible;
    private bool _windowUnavailable;
    private bool _disposed;
    private uint _taskbarCreatedMessage;
    private string _toolTip = string.Empty;
    private TrayMenuText _menu = new(string.Empty, string.Empty, string.Empty);
    private Action? _notificationActivated;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsTrayIcon"/> class.
    /// </summary>
    /// <param name="logger">Receives the native failures of the icon and the notifications.</param>
    public WindowsTrayIcon(Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _windowProcedure = OnWindowMessage;
        _className = "JiuMiTray_" + Guid.NewGuid().ToString("N");
    }

    /// <inheritdoc />
    public event EventHandler? ShowRequested;

    /// <inheritdoc />
    public event EventHandler? SettingsRequested;

    /// <inheritdoc />
    public event EventHandler? ExitRequested;

    /// <inheritdoc />
    public bool Show(string toolTip, TrayMenuText menu)
    {
        ArgumentNullException.ThrowIfNull(toolTip);
        ArgumentNullException.ThrowIfNull(menu);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _toolTip = toolTip;
        _menu = menu;

        EnsureWindow();
        if (_window == IntPtr.Zero)
        {
            return false;
        }

        if (_visible)
        {
            UpdateIcon();
            return true;
        }

        return AddIcon();
    }

    /// <inheritdoc />
    public void Show(string title, string message, Action? activated)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        if (_disposed || !_visible)
        {
            // Notifications only exist while the icon is up; dropping one keeps a missing tray harmless.
            _logger.Debug("Dropped a user notification because the tray icon is not shown");
            return;
        }

        _notificationActivated = activated;

        NotifyIconData data = CreateData(NotifyFlagInfo);
        data.InfoTitle = Truncate(title, InfoTitleLength);
        data.Info = Truncate(message, InfoLength);
        data.InfoFlags = NotifyInfoInfo;
        data.TimeoutOrVersion = 5000;

        if (!Shell_NotifyIconW(NotifyModify, ref data))
        {
            _logger.Warning("The user notification could not be shown");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notificationActivated = null;

        if (_visible)
        {
            NotifyIconData data = CreateData(uFlags: 0);
            _ = Shell_NotifyIconW(NotifyDelete, ref data);
            _visible = false;
        }

        if (_window != IntPtr.Zero)
        {
            _ = DestroyWindow(_window);
            _window = IntPtr.Zero;
            _ = UnregisterClassW(_className, GetModuleHandleW(null));
        }

        if (_icon != IntPtr.Zero)
        {
            if (_ownsIcon)
            {
                _ = DestroyIcon(_icon);
            }

            _icon = IntPtr.Zero;
        }
    }

    private void EnsureWindow()
    {
        if (_window != IntPtr.Zero || _windowUnavailable)
        {
            return;
        }

        IntPtr module = GetModuleHandleW(null);
        var windowClass = new WindowClass
        {
            WindowProc = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            Instance = module,
            ClassName = _className,
        };

        if (RegisterClassW(ref windowClass) == 0)
        {
            _windowUnavailable = true;
            _logger.Warning("The tray window class could not be registered");
            return;
        }

        _window = CreateWindowExW(
            dwExStyle: 0,
            lpClassName: _className,
            lpWindowName: "JiuMi",
            dwStyle: 0,
            x: 0,
            y: 0,
            width: 0,
            height: 0,
            // A hidden top-level window: it has no taskbar entry of its own, but it can take the
            // foreground, which the context menu needs in order to dismiss correctly.
            hWndParent: IntPtr.Zero,
            hMenu: IntPtr.Zero,
            hInstance: module,
            lpParam: IntPtr.Zero);

        if (_window == IntPtr.Zero)
        {
            _ = UnregisterClassW(_className, module);
            _windowUnavailable = true;
            _logger.Warning("The tray message window could not be created");
            return;
        }

        _icon = LoadApplicationIcon();
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
    }

    private IntPtr LoadApplicationIcon()
    {
        string? executablePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(executablePath) &&
            ExtractIconExW(executablePath, 0, out IntPtr large, out IntPtr small, 1) > 0)
        {
            if (large != IntPtr.Zero)
            {
                _ = DestroyIcon(large);
            }

            if (small != IntPtr.Zero)
            {
                _ownsIcon = true;
                return small;
            }
        }

        return LoadIconW(IntPtr.Zero, _applicationIconId);
    }

    private bool AddIcon()
    {
        NotifyIconData data = CreateData(NotifyFlagMessage | NotifyFlagIcon | NotifyFlagTip);
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon;
        data.Tip = Truncate(_toolTip, TipLength);

        if (!Shell_NotifyIconW(NotifyAdd, ref data))
        {
            _logger.Warning("The tray icon could not be added; the notification area may not be ready");
            return false;
        }

        _visible = true;
        return true;
    }

    private void UpdateIcon()
    {
        NotifyIconData data = CreateData(NotifyFlagTip);
        data.Tip = Truncate(_toolTip, TipLength);

        if (!Shell_NotifyIconW(NotifyModify, ref data))
        {
            _logger.Debug("The tray tooltip could not be updated");
        }
    }

    private NotifyIconData CreateData(uint uFlags) =>
        new()
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = _window,
            IconId = IconId,
            Flags = uFlags,
            Tip = string.Empty,
            Info = string.Empty,
            InfoTitle = string.Empty,
        };

    private void ShowMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            _logger.Debug("The tray menu could not be created");
            return;
        }

        try
        {
            _ = AppendMenuW(menu, MenuString, new UIntPtr(CommandShow), _menu.Show);
            _ = AppendMenuW(menu, MenuString, new UIntPtr(CommandSettings), _menu.Settings);
            _ = AppendMenuW(menu, MenuString, new UIntPtr(CommandExit), _menu.Exit);

            if (!GetCursorPos(out Point point))
            {
                return;
            }

            // The foreground window has to change first, otherwise the menu does not close when the
            // user clicks elsewhere.
            _ = SetForegroundWindow(_window);
            int command = TrackPopupMenuEx(
                menu,
                TrackRightButton | TrackNoNotify | TrackReturnCommand,
                point.X,
                point.Y,
                _window,
                IntPtr.Zero);
            _ = PostMessageW(_window, WmNull, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case (int)CommandShow:
                    Raise(ShowRequested);
                    break;
                case (int)CommandSettings:
                    Raise(SettingsRequested);
                    break;
                case (int)CommandExit:
                    Raise(ExitRequested);
                    break;
            }
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    private IntPtr OnWindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == CallbackMessage)
        {
            switch ((int)lParam.ToInt64())
            {
                case WmLeftButtonUp:
                case WmLeftButtonDoubleClick:
                    Raise(ShowRequested);
                    return IntPtr.Zero;

                case WmRightButtonUp:
                case WmContextMenu:
                    ShowMenu();
                    return IntPtr.Zero;

                case NinBalloonUserClick:
                    Action? activated = _notificationActivated;
                    _notificationActivated = null;
                    InvokeActivated(activated);
                    return IntPtr.Zero;

                case NinBalloonHide:
                case NinBalloonTimeout:
                    _notificationActivated = null;
                    return IntPtr.Zero;

                default:
                    return IntPtr.Zero;
            }
        }

        if (_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage)
        {
            // Explorer restarted; the icon belongs to it, so it has to be added again.
            _visible = false;
            _ = AddIcon();
            return IntPtr.Zero;
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The window procedure is a native boundary; a failing handler must not escape into it.")]
    private void Raise(EventHandler? handler)
    {
        try
        {
            handler?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            _logger.Debug(exception, "A tray command handler failed");
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The window procedure is a native boundary; a failing handler must not escape into it.")]
    private void InvokeActivated(Action? activated)
    {
        try
        {
            activated?.Invoke();
        }
        catch (Exception exception)
        {
            _logger.Debug(exception, "A notification activation handler failed");
        }
    }

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..length];

    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public IntPtr WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string? ClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint IconId;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern ushort RegisterClassW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int width,
        int height,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint RegisterWindowMessageW(string message);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr itemId, string item);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr window,
        IntPtr parameters);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint ExtractIconExW(
        string file,
        int iconIndex,
        out IntPtr large,
        out IntPtr small,
        uint icons);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);
}
