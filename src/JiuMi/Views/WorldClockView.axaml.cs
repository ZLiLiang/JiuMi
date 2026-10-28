using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The world clock page. It positions the current-instant line and the cursor band after layout.
/// </summary>
public partial class WorldClockView : UserControl
{
    private WorldClockViewModel? _viewModel;
    private bool _isSubscribed;
    private bool _isAttached;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockView"/> class.
    /// </summary>
    public WorldClockView()
    {
        InitializeComponent();
        TimelineOverlay.SizeChanged += OnTimelineOverlaySizeChanged;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Subscribe();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        Unsubscribe();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Unsubscribe();
        _viewModel = DataContext as WorldClockViewModel;
        Subscribe();
        PostUpdateOverlay();
    }

    private void Subscribe()
    {
        if (_viewModel is null || _isSubscribed || !_isAttached)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ManageRequested += OnManageRequested;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (_viewModel is null || !_isSubscribed)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ManageRequested -= OnManageRequested;
        _isSubscribed = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorldClockViewModel.NowFraction)
            or nameof(WorldClockViewModel.CursorColumn)
            or nameof(WorldClockViewModel.IsPlanView)
            or nameof(WorldClockViewModel.Rows))
        {
            PostUpdateOverlay();
        }
    }

    private void OnTimelineOverlaySizeChanged(object? sender, SizeChangedEventArgs e) => UpdateOverlay();

    private void PostUpdateOverlay() =>
        Dispatcher.UIThread.Post(UpdateOverlay, DispatcherPriority.Loaded);

    private void UpdateOverlay()
    {
        if (_viewModel is null ||
            TimelineOverlay.Bounds.Width <= 0 ||
            TimelineOverlay.Bounds.Height <= 0)
        {
            return;
        }

        double width = TimelineOverlay.Bounds.Width;
        double height = TimelineOverlay.Bounds.Height;
        double nowX = width * Math.Clamp(_viewModel.NowFraction, 0, 1);

        Canvas.SetLeft(NowLine, nowX - 1);
        Canvas.SetTop(NowLine, 0);
        NowLine.Height = height;

        double tagWidth = NowTag.Bounds.Width > 0 ? NowTag.Bounds.Width : 64;
        double tagLeft = _viewModel.NowLabelFlip ? nowX - tagWidth - 4 : nowX + 4;
        Canvas.SetLeft(NowTag, Math.Clamp(tagLeft, 0, Math.Max(0, width - tagWidth)));
        Canvas.SetTop(NowTag, 2);

        double cellWidth = width / 24;
        Canvas.SetLeft(CursorBand, _viewModel.CursorColumn * cellWidth);
        CursorBand.Width = cellWidth;
        double rowsTop = TimelineRows.TranslatePoint(new Point(0, 0), TimelineOverlay)?.Y ?? 0;
        Canvas.SetTop(CursorBand, rowsTop);
        CursorBand.Height = Math.Max(0, height - rowsTop);
    }

    private async void OnManageRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        try
        {
            _viewModel.Manage.PrepareForOpen();
            var window = new WorldClockManageWindow { DataContext = _viewModel.Manage };
            await window.ShowDialog(owner);
        }
        catch (Exception exception)
        {
            // The dialog is outside the page's data path; record it instead of leaving the dispatcher.
            _viewModel.ReportManageFailure(exception);
        }
    }
}
