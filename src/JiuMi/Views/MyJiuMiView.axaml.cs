using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using JiuMi.Application.MyJiuMi;
using JiuMi.MyJiuMi;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The My JiuMi page: a minimal room whose cat follows pointer clicks, drags and floor summons.
/// </summary>
public partial class MyJiuMiView : UserControl, IMyJiuMiHomeHost
{
    private readonly ScaleTransform _homeScale = new(1, 1);
    private readonly ScaleTransform _motionScale = new(1, 1);
    private readonly RotateTransform _motionRotation = new(0);
    private readonly TranslateTransform _motionOffset = new(0, 0);
    private readonly ScaleTransform _shadowScale = new(1, 1);
    private readonly TransformGroup _motionTransform = new();
    private MyJiuMiViewModel? _viewModel;
    private Window? _window;
    private OverlayLayer? _dragLayer;
    private Image? _dragCat;
    private Vector _dragGrabOffset;
    private IPointer? _pointer;
    private Point _pressOrigin;
    private double _dragOffsetX;
    private PixelPoint? _catScreenAnchor;
    private bool _isDragOverlayActive;
    private bool _isDragPreviewActive;
    private bool _isDragPreviewUnavailable;
    private bool _isPressed;
    private bool _isDragging;
    private bool _isSubscribed;
    private bool _isAttached;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiView"/> class.
    /// </summary>
    public MyJiuMiView()
    {
        InitializeComponent();
        _motionTransform.Children = [_motionScale, _motionRotation, _motionOffset];
        CatStage.RenderTransform = _homeScale;
        CatRoot.RenderTransform = _motionTransform;
        CatShadow.RenderTransform = _shadowScale;
        ApplyMotion();
        RoomWall.EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    /// <inheritdoc />
    public bool IsHomeVisible =>
        _isAttached &&
        _window is { IsVisible: true } &&
        _window.WindowState != WindowState.Minimized;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
            _window.Deactivated += OnWindowDeactivated;
            _window.PositionChanged += OnWindowPositionChanged;
        }

        _isAttached = true;
        Subscribe();
        ApplyMirror();
        ApplyScale();
        ApplyMotion();
        UpdateActivation();
        _viewModel?.Session.AttachHomeHost(this);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        Unsubscribe();
        _viewModel?.Session.DetachHomeHost(this);
        _catScreenAnchor = null;
        EndDragOverlay();
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window.Deactivated -= OnWindowDeactivated;
            _window.PositionChanged -= OnWindowPositionChanged;
            _window = null;
        }

        EndActiveDrag();
        _viewModel?.Deactivate();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Unsubscribe();
        _viewModel?.Session.DetachHomeHost(this);
        _viewModel = DataContext as MyJiuMiViewModel;
        Subscribe();
        _viewModel?.Session.AttachHomeHost(this);
        ApplyMirror();
        ApplyScale();
        ApplyMotion();
        UpdateActivation();
    }

    /// <inheritdoc />
    public PixelPoint? GetCatScreenAnchor()
    {
        if (IsHomeVisible)
        {
            UpdateCatScreenAnchor();
        }

        return _catScreenAnchor;
    }

    /// <inheritdoc />
    public MyJiuMiScreenRect? GetRoomScreenRect()
    {
        if (GetRoomRectInWindow() is not { } room || _window is null)
        {
            return null;
        }

        PixelPoint topLeft = _window.PointToScreen(room.TopLeft);
        PixelPoint bottomRight = _window.PointToScreen(room.BottomRight);
        var screen = new MyJiuMiScreenRect(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
        return screen.IsEmpty ? null : screen;
    }

    private Rect? GetRoomRectInWindow()
    {
        if (_window is null ||
            !IsHomeVisible ||
            Room.Bounds.Width <= 0 ||
            Room.Bounds.Height <= 0)
        {
            return null;
        }

        Point? topLeft = Room.TranslatePoint(default, _window);
        Point? bottomRight = Room.TranslatePoint(
            new Point(Room.Bounds.Width, Room.Bounds.Height),
            _window);
        if (topLeft is not { } start || bottomRight is not { } end)
        {
            return null;
        }

        var rect = new Rect(start.X, start.Y, end.X - start.X, end.Y - start.Y);

        // The room may be scrolled inside the page; the clipped-away part does not count.
        if (this.FindAncestorOfType<ScrollViewer>() is { } scroller &&
            scroller.Bounds.Width > 0 &&
            scroller.TranslatePoint(default, _window) is { } viewportOrigin)
        {
            rect = rect.Intersect(new Rect(viewportOrigin, scroller.Bounds.Size));
        }

        rect = rect.Intersect(new Rect(_window.ClientSize));
        return rect.Width > 0 && rect.Height > 0 ? rect : null;
    }

    private void Subscribe()
    {
        if (_viewModel is null || _isSubscribed)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (_viewModel is null || !_isSubscribed)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isSubscribed = false;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            UpdateActivation();
            return;
        }

        if (e.Property == Window.WindowStateProperty)
        {
            // Minimizing keeps the cat running until the shell decides whether it walks out; hiding
            // deactivates it here because the page cannot be seen any more.
            if (_window?.WindowState != WindowState.Minimized)
            {
                UpdateActivation();
            }
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => UpdateCatScreenAnchor();

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e) =>
        UpdateCatScreenAnchor();

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        // Minimized windows move to an off-screen sentinel; that position is not the cat's anchor.
        if (e.Point.X <= -10000 || e.Point.Y <= -10000)
        {
            return;
        }

        UpdateCatScreenAnchor();
    }

    private void OnRoomWallSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateActivation();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MyJiuMiViewModel.IsFacingRight))
        {
            ApplyMirror();
        }

        if (e.PropertyName == nameof(MyJiuMiViewModel.HomeScale))
        {
            ApplyScale();
        }

        if (e.PropertyName == nameof(MyJiuMiViewModel.Motion))
        {
            ApplyMotion();
        }

        if (e.PropertyName == nameof(MyJiuMiViewModel.Frame) && _dragCat is not null)
        {
            _dragCat.Source = _viewModel?.Frame;
        }

        if (e.PropertyName is nameof(MyJiuMiViewModel.CatMargin) or nameof(MyJiuMiViewModel.IsCatVisible))
        {
            UpdateCatScreenAnchor();
        }
    }

    private void ApplyMirror()
    {
        CatImage.RenderTransform = _viewModel?.IsFacingRight == true
            ? new ScaleTransform(-1, 1)
            : null;
    }

    private void ApplyScale()
    {
        double scale = _viewModel?.HomeScale ?? 1;
        _homeScale.ScaleX = scale;
        _homeScale.ScaleY = scale;
    }

    private void ApplyMotion()
    {
        MyJiuMiMotion motion = _viewModel?.Motion ?? MyJiuMiMotion.None;
        _motionScale.ScaleX = motion.ScaleX;
        _motionScale.ScaleY = motion.ScaleY;
        _motionRotation.Angle = motion.RotationDegrees;
        _motionOffset.Y = motion.OffsetY;
        _shadowScale.ScaleX = motion.ShadowScale;
        _shadowScale.ScaleY = motion.ShadowScale;
        CatShadow.Opacity = motion.ShadowOpacity;
    }

    private void UpdateActivation()
    {
        // The page warmer measures and arranges a view before it joins the visual tree; only an
        // attached view may start the animation clock.
        if (_viewModel is null || !_isAttached)
        {
            return;
        }

        double width = RoomWall.Bounds.Width;
        if (width > 0)
        {
            _viewModel.SetStageWidth(width);
        }

        if (_window is { IsVisible: false })
        {
            if (_viewModel.IsActive)
            {
                EndActiveDrag();
                _viewModel.Deactivate();
            }

            return;
        }

        if (_window?.WindowState == WindowState.Minimized)
        {
            // The shell decides between walking out and pausing; the page must not stop the cat first.
            return;
        }

        if (width > 0 && !_viewModel.IsActive)
        {
            _viewModel.Activate(width);
        }

        UpdateCatScreenAnchor();
    }

    private void UpdateCatScreenAnchor()
    {
        if (_viewModel is null ||
            _window is null ||
            !_viewModel.IsCatRootVisible ||
            _window.WindowState == WindowState.Minimized ||
            RoomWall.Bounds.Width <= 0)
        {
            return;
        }

        double x = Math.Clamp(
            _viewModel.CatMargin.Left + (MyJiuMiInteractionRules.CatSize / 2),
            0,
            RoomWall.Bounds.Width);
        Point? inWindow = RoomWall.TranslatePoint(new Point(x, RoomWall.Bounds.Height), _window);
        if (inWindow is { } point)
        {
            _catScreenAnchor = _window.PointToScreen(point);
        }
    }

    private void OnCatPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isPressed ||
            _viewModel is null ||
            !e.GetCurrentPoint(CatHitArea).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPressed = true;
        _isDragging = false;
        _pointer = e.Pointer;
        _pressOrigin = e.GetPosition(RoomWall);
        e.Pointer.Capture(CatHitArea);
        e.Handled = true;
    }

    private void OnCatPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPressed || _viewModel is null)
        {
            return;
        }

        Point current = e.GetPosition(RoomWall);
        if (!_isDragging)
        {
            double deltaX = current.X - _pressOrigin.X;
            double deltaY = current.Y - _pressOrigin.Y;
            double threshold = MyJiuMiInteractionRules.DragThreshold;
            if ((deltaX * deltaX) + (deltaY * deltaY) < threshold * threshold)
            {
                return;
            }

            _isDragging = true;
            _dragOffsetX = _pressOrigin.X - _viewModel.CatCenterX;
            _viewModel.BeginDrag();
            BeginDragOverlay(e);
        }

        if (RoomWall.Bounds.Width > 0)
        {
            double targetCenter = current.X - _dragOffsetX;
            _viewModel.DragTo(targetCenter / RoomWall.Bounds.Width);
        }

        UpdateDragOverlay(e);
        e.Handled = true;
    }

    private void OnCatPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed || _viewModel is null)
        {
            return;
        }

        bool wasDragging = _isDragging;
        ReleasePointer();

        if (wasDragging)
        {
            ReleaseDraggedCat(e);
        }
        else
        {
            _viewModel.Click();
        }

        e.Handled = true;
    }

    private void BeginDragOverlay(PointerEventArgs e)
    {
        _isDragPreviewActive = false;
        _isDragPreviewUnavailable = false;
        if (_window is null ||
            _viewModel is null ||
            CatStage.TranslatePoint(default, _window) is not { } origin)
        {
            return;
        }

        _dragGrabOffset = e.GetPosition(_window) - origin;
        _dragLayer = OverlayLayer.GetOverlayLayer(this);
        if (_dragLayer is null)
        {
            // Without a window overlay the cat stays in the room; the drag still updates its X.
            return;
        }

        _dragCat = new Image
        {
            Width = MyJiuMiInteractionRules.CatSize,
            Height = MyJiuMiInteractionRules.CatSize,
            Stretch = Stretch.Uniform,
            Source = _viewModel.Frame,
            IsHitTestVisible = false,
        };
        RenderOptions.SetBitmapInterpolationMode(_dragCat, BitmapInterpolationMode.HighQuality);
        _dragLayer.Children.Add(_dragCat);

        // The lifted cat is a window-level copy; the room cat stays in place but invisible so the
        // copy can cross the room border and the page header.
        _isDragOverlayActive = true;
        CatStage.Opacity = 0;
        UpdateDragOverlay(e);
    }

    private void UpdateDragOverlay(PointerEventArgs e)
    {
        if (!_isDragOverlayActive || _dragCat is not { } cat || _window is null)
        {
            return;
        }

        Point topLeft = e.GetPosition(_window) - _dragGrabOffset;
        Canvas.SetLeft(cat, topLeft.X);
        Canvas.SetTop(cat, topLeft.Y);

        // Once any part of the cat slot leaves the window, a temporary desktop host keeps the cat
        // visible outside; the page window still owns the pointer capture until the release.
        Rect client = new(_window.ClientSize);
        double size = MyJiuMiInteractionRules.CatSize;
        bool inside = topLeft.X >= client.Left &&
            topLeft.Y >= client.Top &&
            topLeft.X + size <= client.Right &&
            topLeft.Y + size <= client.Bottom;
        UpdateDragPreview(topLeft, inside);
    }

    private void UpdateDragPreview(Point topLeft, bool inside)
    {
        if (_viewModel is null || _window is null)
        {
            return;
        }

        if (inside)
        {
            if (_isDragPreviewActive)
            {
                // Show the overlay copy first so the hand-back never leaves an empty frame.
                if (_dragCat is not null)
                {
                    _dragCat.IsVisible = true;
                }

                _viewModel.CloseDragPreview();
                _isDragPreviewActive = false;
            }

            return;
        }

        if (_isDragPreviewUnavailable)
        {
            return;
        }

        double size = MyJiuMiInteractionRules.CatSize;
        PixelPoint anchor = _window.PointToScreen(new Point(
            topLeft.X + (size / 2),
            topLeft.Y + size));

        if (!_isDragPreviewActive)
        {
            _isDragPreviewActive = _viewModel.ShowDragPreview(anchor);
            if (_isDragPreviewActive)
            {
                if (_dragCat is not null)
                {
                    _dragCat.IsVisible = false;
                }
            }
            else
            {
                _isDragPreviewUnavailable = true;
            }

            return;
        }

        if (!_viewModel.MoveDragPreview(anchor))
        {
            _isDragPreviewActive = false;
            if (_dragCat is not null)
            {
                _dragCat.IsVisible = true;
            }
        }
    }

    private void EndDragOverlay()
    {
        if (_isDragPreviewActive)
        {
            _viewModel?.CloseDragPreview();
            _isDragPreviewActive = false;
        }

        if (_dragCat is { } cat && _dragLayer is { } layer)
        {
            layer.Children.Remove(cat);
        }

        _dragCat = null;
        _dragLayer = null;
        if (_isDragOverlayActive)
        {
            _isDragOverlayActive = false;
            CatStage.Opacity = 1;
        }
    }

    private void ReleaseDraggedCat(PointerReleasedEventArgs e)
    {
        if (_viewModel is null || _window is null)
        {
            EndDragOverlay();
            _viewModel?.EndDrag();
            return;
        }

        Point pointer = e.GetPosition(_window);
        Point topLeft = pointer - _dragGrabOffset;
        double catSize = MyJiuMiInteractionRules.CatSize;
        PixelPoint? anchor = _isDragOverlayActive
            ? _window.PointToScreen(new Point(
                topLeft.X + (catSize / 2),
                topLeft.Y + catSize))
            : GetCatScreenAnchor();
        PixelPoint pointerScreen = _window.PointToScreen(pointer);
        bool commits = anchor is not null &&
            GetRoomScreenRect() is { } room &&
            MyJiuMiTransitionRules.ShouldCommitDragOut(
                room,
                new MyJiuMiScreenPoint(pointerScreen.X, pointerScreen.Y),
                _window.RenderScaling);

        if (commits)
        {
            // The preview is adopted by the session and only then is the overlay cleaned up, so the
            // cat never disappears between the two hosts. A failed handoff is bounced by the session.
            _viewModel.TryHandOffToDesktop(anchor!.Value);
            EndDragOverlay();
            return;
        }

        EndDragOverlay();
        _viewModel.EndDrag();
    }

    private void OnCatPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_isPressed)
        {
            return;
        }

        EndActiveDrag();
    }

    private void EndActiveDrag()
    {
        bool wasDragging = _isDragging;
        ReleasePointer();
        EndDragOverlay();

        if (wasDragging)
        {
            _viewModel?.EndDrag();
        }
    }

    private void ReleasePointer()
    {
        _isPressed = false;
        _isDragging = false;
        IPointer? pointer = _pointer;
        _pointer = null;
        pointer?.Capture(null);
    }

    private void OnRoomDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.IsActive || RoomWall.Bounds.Width <= 0)
        {
            return;
        }

        if (e.Source == CatHitArea)
        {
            // A double click on the cat is two single clicks, not a summon.
            return;
        }

        _viewModel.Summon(e.GetPosition(RoomWall).X / RoomWall.Bounds.Width);
        e.Handled = true;
    }
}
