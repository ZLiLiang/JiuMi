using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JiuMi.Application.Pet;

namespace JiuMi.Views;

/// <summary>
/// The transparent, always-on-top desktop pet window.
/// </summary>
/// <remarks>
/// The window only renders the current frame and its vector overlays, and turns pointer input into
/// clicks and drags. Placement, entrance, persistence and the animation state belong to
/// <see cref="Pet.PetWindowController"/>.
/// </remarks>
public partial class PetWindow : Window
{
    private const double DragThresholdDip = 4d;

    private readonly ScaleTransform _mirror = new(-1d, 1d);

    private PixelPoint _pointerStartScreen;
    private PixelPoint _windowStart;
    private bool _isPressed;
    private bool _isDragging;
    private bool _isMirrored;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetWindow"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader.</remarks>
    public PetWindow()
    {
        InitializeComponent();
        Width = PetPlacement.SizeDip;
        Height = PetPlacement.SizeDip;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    /// <summary>
    /// Raised when a pointer press starts an interaction that interrupts the entrance.
    /// </summary>
    public event EventHandler? InteractionStarted;

    /// <summary>
    /// Raised once a press has moved far enough to become a drag.
    /// </summary>
    public event EventHandler? DragStarted;

    /// <summary>
    /// Raised when a press is released without becoming a drag.
    /// </summary>
    public event EventHandler? Clicked;

    /// <summary>
    /// Raised when a drag ends, so the new position can be clamped and saved.
    /// </summary>
    public event EventHandler? DragCompleted;

    /// <summary>
    /// Shows a frame in the window.
    /// </summary>
    /// <param name="frame">The image to draw.</param>
    public void SetFrame(IImage frame) => Frame.Source = frame;

    /// <summary>
    /// Shows or hides the vector heart overlay.
    /// </summary>
    /// <param name="visible">Whether the heart is visible.</param>
    public void SetHeartVisible(bool visible) => Heart.IsVisible = visible;

    /// <summary>
    /// Shows or hides the vector <c>Zzz</c> overlay.
    /// </summary>
    /// <param name="visible">Whether the sleep marks are visible.</param>
    public void SetSleeping(bool visible) => SleepMarks.IsVisible = visible;

    /// <summary>
    /// Mirrors the walking frames for an entrance that moves to the right, since the exported walk
    /// cycle faces left.
    /// </summary>
    /// <param name="mirrored">Whether the frame is drawn flipped horizontally.</param>
    public void SetMirrored(bool mirrored)
    {
        if (_isMirrored == mirrored)
        {
            return;
        }

        _isMirrored = mirrored;
        Frame.RenderTransform = mirrored ? _mirror : null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPressed = true;
        _isDragging = false;
        _pointerStartScreen = this.PointToScreen(e.GetPosition(this));
        _windowStart = Position;
        InteractionStarted?.Invoke(this, EventArgs.Empty);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPressed)
        {
            return;
        }

        PixelPoint current = this.PointToScreen(e.GetPosition(this));
        int deltaX = current.X - _pointerStartScreen.X;
        int deltaY = current.Y - _pointerStartScreen.Y;
        double threshold = DragThresholdDip * RenderScaling;
        if (!_isDragging && Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) >= threshold)
        {
            _isDragging = true;
            DragStarted?.Invoke(this, EventArgs.Empty);
        }

        if (_isDragging)
        {
            Position = new PixelPoint(_windowStart.X + deltaX, _windowStart.Y + deltaY);
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
        {
            return;
        }

        _isPressed = false;
        e.Pointer.Capture(null);
        e.Handled = true;

        if (_isDragging)
        {
            _isDragging = false;
            DragCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
