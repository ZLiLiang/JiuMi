using System;
using System.ComponentModel;
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
    private readonly ScaleTransform _catMirror = new(1, 1);
    private readonly MyJiuMiSession? _session;
    private IPointer? _pointer;
    private PixelPoint _pointerStartScreen;
    private PixelPoint _windowStart;
    private bool _isPressed;
    private bool _isDragging;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiDesktopWindow"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader.</remarks>
    public MyJiuMiDesktopWindow()
    {
        InitializeComponent();
        CatImage.RenderTransform = _catMirror;
        Closed += OnClosed;
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
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MyJiuMiSession.DesktopOpacity) or
            nameof(MyJiuMiSession.IsFacingRight))
        {
            ApplyAppearance();
        }
    }

    private void ApplyAppearance()
    {
        if (_session is null)
        {
            return;
        }

        Opacity = _session.DesktopOpacity;
        _catMirror.ScaleX = _session.IsFacingRight ? -1 : 1;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null ||
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
        if (_session is null || !_isPressed)
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

        if (wasDragging)
        {
            // A release inside the visible room hands the cat back; otherwise the window stays on
            // the desktop and is clamped into the nearest work area.
            PixelPoint release = this.PointToScreen(e.GetPosition(this));
            PixelPoint anchor = this.PointToScreen(new Point(Bounds.Width / 2, Bounds.Height));
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
        if (wasDragging)
        {
            _session.EndDesktopDrag();
            _session.ClampDesktopWindow();
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_session is null)
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
}
