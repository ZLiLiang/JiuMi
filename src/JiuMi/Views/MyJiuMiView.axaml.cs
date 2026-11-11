using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JiuMi.Application.MyJiuMi;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The My JiuMi page: a minimal room whose cat follows pointer clicks, drags and floor summons.
/// </summary>
public partial class MyJiuMiView : UserControl
{
    private MyJiuMiViewModel? _viewModel;
    private Window? _window;
    private IPointer? _pointer;
    private Point _pressOrigin;
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
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
        }

        _isAttached = true;
        Subscribe();
        UpdateActivation();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        Unsubscribe();
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
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
        _viewModel = DataContext as MyJiuMiViewModel;
        Subscribe();
        ApplyMirror();
        UpdateActivation();
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
        if (e.Property == Window.WindowStateProperty || e.Property == Visual.IsVisibleProperty)
        {
            UpdateActivation();
        }
    }

    private void OnRoomWallSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateActivation();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MyJiuMiViewModel.IsFacingRight))
        {
            ApplyMirror();
        }
    }

    private void ApplyMirror()
    {
        CatImage.RenderTransform = _viewModel?.IsFacingRight == true
            ? new ScaleTransform(-1, 1)
            : null;
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

        bool isVisible = _window is null ||
            (_window.IsVisible && _window.WindowState != WindowState.Minimized);

        if (!isVisible)
        {
            if (_viewModel.IsActive)
            {
                EndActiveDrag();
                _viewModel.Deactivate();
            }

            return;
        }

        if (width > 0 && !_viewModel.IsActive)
        {
            _viewModel.Activate(width);
        }
    }

    private void OnCatPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isPressed ||
            _viewModel is null ||
            !e.GetCurrentPoint(CatRoot).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPressed = true;
        _isDragging = false;
        _pointer = e.Pointer;
        _pressOrigin = e.GetPosition(RoomWall);
        e.Pointer.Capture(CatRoot);
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
            if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) < MyJiuMiInteractionRules.DragThreshold)
            {
                return;
            }

            _isDragging = true;
            _viewModel.BeginDrag();
        }

        if (RoomWall.Bounds.Width > 0)
        {
            _viewModel.DragTo(current.X / RoomWall.Bounds.Width);
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
        _isPressed = false;
        _isDragging = false;
        IPointer? pointer = _pointer;
        _pointer = null;
        pointer?.Capture(null);

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
        _isPressed = false;
        _isDragging = false;
        IPointer? pointer = _pointer;
        _pointer = null;
        pointer?.Capture(null);

        if (wasDragging)
        {
            _viewModel?.EndDrag();
        }
    }

    private void OnRoomDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.IsActive || RoomWall.Bounds.Width <= 0)
        {
            return;
        }

        Point local = e.GetPosition(CatRoot);
        if (local.X is >= 0 and <= MyJiuMiInteractionRules.CatSize &&
            local.Y is >= 0 and <= MyJiuMiInteractionRules.CatSize)
        {
            // A double click on the cat is two single clicks, not a summon.
            return;
        }

        _viewModel.Summon(e.GetPosition(RoomWall).X / RoomWall.Bounds.Width);
        e.Handled = true;
    }
}
