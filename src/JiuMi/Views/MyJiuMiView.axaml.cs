using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
    private MyJiuMiViewModel? _viewModel;
    private Window? _window;
    private IPointer? _pointer;
    private Point _pressOrigin;
    private double _dragOffsetX;
    private PixelPoint? _catScreenAnchor;
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
        CatRoot.RenderTransform = _homeScale;
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
            if (Math.Abs(current.X - _pressOrigin.X) < MyJiuMiInteractionRules.DragThreshold)
            {
                return;
            }

            _isDragging = true;
            _dragOffsetX = _pressOrigin.X - _viewModel.CatCenterX;
            _viewModel.BeginDrag();
        }

        if (RoomWall.Bounds.Width > 0)
        {
            double targetCenter = current.X - _dragOffsetX;
            _viewModel.DragTo(targetCenter / RoomWall.Bounds.Width);
        }

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
            _viewModel.EndDrag();
        }
        else
        {
            _viewModel.Click();
        }

        e.Handled = true;
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
