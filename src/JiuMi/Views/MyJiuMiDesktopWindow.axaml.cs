using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JiuMi.Application.MyJiuMi;
using JiuMi.MyJiuMi;

namespace JiuMi.Views;

/// <summary>
/// The transparent, always-on-top desktop host: only the cat and its vector overlays are drawn.
/// </summary>
/// <remarks>
/// The window renders the shared session and turns pointer input into clicks, drags of this window
/// and the double-click that restores the My JiuMi page. Placement and the state machine belong to
/// <see cref="MyJiuMiSession"/>.
/// </remarks>
public partial class MyJiuMiDesktopWindow : Window
{
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WmActivate = 0x0006;
    private const uint WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const int WaActive = 1;

    private readonly ScaleTransform _motionScale = new(1, 1);
    private readonly RotateTransform _motionRotation = new(0);
    private readonly TransformGroup _motionTransform = new();
    private readonly TranslateTransform _catAnchorOffset = new(0, 0);
    private readonly ScaleTransform _catMirror = new(1, 1);
    private readonly TransformGroup _catImageTransform = new();
    private readonly MyJiuMiSession? _session;
    private IPointer? _pointer;
    private PixelPoint _pointerStartScreen;
    private PixelPoint _windowStart;
    private IntPtr _foregroundBeforeClick;
    private bool _isPressed;
    private bool _isDragging;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiDesktopWindow"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader.</remarks>
    public MyJiuMiDesktopWindow()
    {
        InitializeComponent();
        _motionTransform.Children = [_motionScale, _motionRotation];
        _catImageTransform.Children = [_catAnchorOffset, _catMirror];
        CatRoot.RenderTransform = _motionTransform;
        CatImage.RenderTransform = _catImageTransform;
        ApplyMotion();
        Closed += OnClosed;

        if (OperatingSystem.IsWindows())
        {
            // Avalonia calls SetFocus from WM_LBUTTONDOWN, which bypasses WS_EX_NOACTIVATE when the
            // foreground window belongs to this process; WM_ACTIVATE puts the previous window back.
            Win32Properties.AddWindowStylesCallback(
                this,
                (style, exStyle) => (style, exStyle | WsExNoActivate | WsExToolWindow));
            Win32Properties.AddWndProcHookCallback(this, OnWndProc);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiDesktopWindow"/> class for a session.
    /// </summary>
    /// <param name="session">The shared session that owns the frames and interactions.</param>
    public MyJiuMiDesktopWindow(MyJiuMiSession session)
        : this()
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        DataContext = session;
        session.PropertyChanged += OnSessionPropertyChanged;
        ApplyAppearance();
        ApplyMotion();
    }

    private IntPtr OnWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            _foregroundBeforeClick = GetForegroundWindow();
            handled = true;
            return (IntPtr)MaNoActivate;
        }

        if (msg == WmActivate && wParam == (IntPtr)WaActive)
        {
            RestoreForeground(hWnd);
        }

        return IntPtr.Zero;
    }

    private void RestoreForeground(IntPtr hWnd)
    {
        IntPtr previous = _foregroundBeforeClick;
        _foregroundBeforeClick = IntPtr.Zero;
        if (previous == IntPtr.Zero ||
            previous == hWnd ||
            GetForegroundWindow() != hWnd ||
            !IsWindow(previous))
        {
            return;
        }

        SetForegroundWindow(previous);
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MyJiuMiSession.DesktopOpacity) or
            nameof(MyJiuMiSession.IsFacingRight) or
            nameof(MyJiuMiSession.FrameAnchor))
        {
            ApplyAppearance();
        }

        if (e.PropertyName == nameof(MyJiuMiSession.Motion))
        {
            ApplyMotion();
        }
    }

    private void ApplyMotion()
    {
        MyJiuMiMotion motion = _session?.Motion ?? MyJiuMiMotion.None;
        _motionScale.ScaleX = motion.ScaleX;
        _motionScale.ScaleY = motion.ScaleY;
        _motionRotation.Angle = motion.RotationDegrees;
    }

    private void ApplyAppearance()
    {
        if (_session is null)
        {
            return;
        }

        Opacity = _session.DesktopOpacity;
        double slotSize = Bounds.Width > 0 ? Bounds.Width : Width;
        (double offsetX, double offsetY) = MyJiuMiAnchorRules.ComputeDesktopFrameDeltaDip(
            _session.FrameAnchor,
            _session.Manifest.DefaultAnchor,
            _session.Manifest.Canvas,
            slotSize);
        _catAnchorOffset.X = offsetX;
        _catAnchorOffset.Y = offsetY;
        _catMirror.ScaleX = _session.IsFacingRight ? -1 : 1;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null ||
            _session.IsSummoning ||
            _isPressed ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPressed = true;
        _isDragging = false;
        _pointer = e.Pointer;
        _pointerStartScreen = this.PointToScreen(e.GetPosition(this));
        _windowStart = Position;
        e.Pointer.Capture(CatRoot);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_session is null || _session.IsSummoning || !_isPressed)
        {
            return;
        }

        PixelPoint current = this.PointToScreen(e.GetPosition(this));
        int deltaX = current.X - _pointerStartScreen.X;
        int deltaY = current.Y - _pointerStartScreen.Y;
        double threshold = MyJiuMiInteractionRules.DragThreshold * RenderScaling;
        if (!_isDragging && (deltaX * deltaX) + (deltaY * deltaY) >= threshold * threshold)
        {
            _isDragging = true;
            _session.BeginDesktopDrag();
        }

        if (_isDragging)
        {
            Position = new PixelPoint(_windowStart.X + deltaX, _windowStart.Y + deltaY);
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_session is null || !_isPressed)
        {
            return;
        }

        bool wasDragging = _isDragging;
        ReleasePointer();

        if (_session.IsSummoning)
        {
            e.Handled = true;
            return;
        }

        if (wasDragging)
        {
            // A release inside the visible room hands the cat back; otherwise the window stays on
            // the desktop and is clamped into the nearest work area.
            PixelPoint release = this.PointToScreen(e.GetPosition(this));
            (double normX, double normY) = MyJiuMiAnchorRules.Normalize(
                _session.Manifest.DefaultAnchor,
                _session.Manifest.Canvas);
            PixelPoint anchor = this.PointToScreen(new Point(Bounds.Width * normX, Bounds.Height * normY));
            _session.EndDesktopDrag();
            if (!_session.TryReturnHomeFromDesktop(release, anchor))
            {
                _session.ClampDesktopWindow();
            }
        }
        else
        {
            _session.Click();
        }

        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_session is null || !_isPressed)
        {
            return;
        }

        bool wasDragging = _isDragging;
        ReleasePointer();
        if (_session.IsSummoning)
        {
            return;
        }

        if (wasDragging)
        {
            _session.EndDesktopDrag();
            _session.ClampDesktopWindow();
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_session is null || _session.IsSummoning)
        {
            return;
        }

        _session.RequestOpenHome();
        e.Handled = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_session is not null)
        {
            _session.PropertyChanged -= OnSessionPropertyChanged;
        }

        Closed -= OnClosed;
    }

    private void ReleasePointer()
    {
        _isPressed = false;
        _isDragging = false;
        IPointer? pointer = _pointer;
        _pointer = null;
        pointer?.Capture(null);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool IsWindow(IntPtr hWnd);
}
