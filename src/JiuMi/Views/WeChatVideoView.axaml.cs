using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JiuMi.Application.WeChatVideo;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The WeChat video download page: the folder picker, the listener switch, thumbnail hover preview
/// and the responsive header.
/// </summary>
public partial class WeChatVideoView : UserControl
{
    private const double NarrowContentWidth = 640;
    private const double TitleBarAndIslandHeight = 68;

    private readonly DispatcherTimer _previewTimer;
    private TopLevel? _topLevel;
    private Window? _window;
    private ScrollViewer? _pageScrollViewer;
    private OverlayLayer? _previewOverlayLayer;
    private Border? _previewCard;
    private Image? _previewImage;
    private Control? _hoveredAnchor;
    private WeChatVideoRowViewModel? _hoveredRow;
    private bool _isNarrow;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoView"/> class.
    /// </summary>
    public WeChatVideoView()
    {
        InitializeComponent();
        _previewTimer = new DispatcherTimer
        {
            Interval = WeChatVideoThumbnailPreviewLayout.HoverDelay,
        };
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _previewTimer.Tick += OnPreviewTimerTick;
        PageContent.SizeChanged += OnPageContentSizeChanged;
        _pageScrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.SizeChanged += OnPageScrollViewerSizeChanged;
            UpdatePageHeight(_pageScrollViewer.Bounds.Height);
        }

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.SizeChanged += OnTopLevelSizeChanged;
            if (_pageScrollViewer is null)
            {
                UpdatePageHeight(_topLevel.ClientSize.Height);
            }
        }

        _window = _topLevel as Window;
        if (_window is not null)
        {
            _window.Deactivated += OnWindowDeactivated;
            _window.PropertyChanged += OnWindowPropertyChanged;
        }

        UpdateHeaderLayout(PageContent.Bounds.Width);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelThumbnailPreview();
        _previewTimer.Tick -= OnPreviewTimerTick;
        PageContent.SizeChanged -= OnPageContentSizeChanged;
        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }

        if (_topLevel is not null)
        {
            _topLevel.SizeChanged -= OnTopLevelSizeChanged;
            _topLevel = null;
        }

        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.SizeChanged -= OnPageScrollViewerSizeChanged;
            _pageScrollViewer = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        CancelThumbnailPreview();
    }

    private void OnPageContentSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateHeaderLayout(e.NewSize.Width);

    private void OnPageScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdatePageHeight(e.NewSize.Height);

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        CancelThumbnailPreview();
        if (_pageScrollViewer is null)
        {
            UpdatePageHeight(e.NewSize.Height - TitleBarAndIslandHeight);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) =>
        CancelThumbnailPreview();

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if ((e.Property == Window.WindowStateProperty && _window?.WindowState == WindowState.Minimized) ||
            (e.Property == IsVisibleProperty && _window?.IsVisible == false))
        {
            CancelThumbnailPreview();
        }
    }

    private void OnListScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta.X != 0 || e.OffsetDelta.Y != 0)
        {
            CancelThumbnailPreview();
        }
    }

    private void OnThumbnailPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control anchor || anchor.DataContext is not WeChatVideoRowViewModel row)
        {
            return;
        }

        if (ReferenceEquals(_hoveredAnchor, anchor))
        {
            return;
        }

        CancelThumbnailPreview();
        _hoveredAnchor = anchor;
        _hoveredRow = row;
        row.PropertyChanged += OnHoveredRowPropertyChanged;

        if (row.Thumbnail is not null)
        {
            _previewTimer.Start();
        }
    }

    private void OnThumbnailPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control anchor && ReferenceEquals(_hoveredAnchor, anchor))
        {
            CancelThumbnailPreview();
        }
    }

    private void OnThumbnailDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control anchor && ReferenceEquals(_hoveredAnchor, anchor))
        {
            CancelThumbnailPreview();
        }
    }

    private void OnHoveredRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_hoveredRow is not { } row)
        {
            return;
        }

        if (e.PropertyName is not (nameof(WeChatVideoRowViewModel.Thumbnail)
            or nameof(WeChatVideoRowViewModel.HasThumbnail)
            or nameof(WeChatVideoRowViewModel.CoverOpacity)
            or null))
        {
            return;
        }

        if (row.Thumbnail is null)
        {
            CancelThumbnailPreview();
            return;
        }

        if (_previewCard is not null)
        {
            TryShowPreviewForHoveredRow();
        }
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        TryShowPreviewForHoveredRow();
    }

    private void TryShowPreviewForHoveredRow()
    {
        if (_hoveredAnchor is not { } anchor ||
            _hoveredRow is not { } row ||
            !anchor.IsPointerOver ||
            row.Thumbnail is not { } thumbnail)
        {
            HidePreviewCard();
            return;
        }

        OverlayLayer? overlayLayer = OverlayLayer.GetOverlayLayer(this);
        if (overlayLayer is null)
        {
            HidePreviewCard();
            return;
        }

        Point? anchorTopLeft = anchor.TranslatePoint(new Point(0, 0), overlayLayer);
        if (anchorTopLeft is not { } origin)
        {
            HidePreviewCard();
            return;
        }

        PixelSize pixelSize = thumbnail.PixelSize;
        Size viewport = overlayLayer.Bounds.Size;
        WeChatVideoThumbnailPreviewPlacement? placement = WeChatVideoThumbnailPreviewLayout.TryCalculate(
            pixelSize.Width,
            pixelSize.Height,
            origin.X,
            origin.Y,
            anchor.Bounds.Width,
            anchor.Bounds.Height,
            viewport.Width,
            viewport.Height);
        if (placement is null)
        {
            HidePreviewCard();
            return;
        }

        ShowPreviewCard(overlayLayer, thumbnail, row.CoverOpacity, placement);
    }

    private void ShowPreviewCard(
        OverlayLayer overlayLayer,
        Bitmap thumbnail,
        double coverOpacity,
        WeChatVideoThumbnailPreviewPlacement placement)
    {
        HidePreviewCard();

        var image = new Image
        {
            Source = thumbnail,
            Width = placement.ImageWidth,
            Height = placement.ImageHeight,
            Stretch = Stretch.Uniform,
            Opacity = coverOpacity,
            IsHitTestVisible = false,
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

        var imageClip = new Border
        {
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true,
            IsHitTestVisible = false,
            Child = image,
        };

        var card = new Border
        {
            Width = placement.CardWidth,
            Height = placement.CardHeight,
            Padding = new Thickness(WeChatVideoThumbnailPreviewLayout.CardPadding),
            BorderThickness = new Thickness(WeChatVideoThumbnailPreviewLayout.CardBorderThickness),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
            Child = imageClip,
        };
        card[!Border.BackgroundProperty] = new DynamicResourceExtension("JiuMiFlySurfaceBrush");
        card[!Border.BorderBrushProperty] = new DynamicResourceExtension("CardStrokeColorDefaultBrush");
        card[!Border.BoxShadowProperty] = new DynamicResourceExtension("JiuMiIslandShadow");

        Canvas.SetLeft(card, placement.Left);
        Canvas.SetTop(card, placement.Top);

        overlayLayer.Children.Add(card);
        _previewOverlayLayer = overlayLayer;
        _previewCard = card;
        _previewImage = image;
    }

    private void HidePreviewCard()
    {
        if (_previewCard is null)
        {
            return;
        }

        _previewOverlayLayer?.Children.Remove(_previewCard);
        if (_previewImage is not null)
        {
            _previewImage.Source = null;
        }

        _previewCard = null;
        _previewImage = null;
        _previewOverlayLayer = null;
    }

    private void CancelThumbnailPreview()
    {
        _previewTimer.Stop();
        HidePreviewCard();
        if (_hoveredRow is not null)
        {
            _hoveredRow.PropertyChanged -= OnHoveredRowPropertyChanged;
            _hoveredRow = null;
        }

        _hoveredAnchor = null;
    }

    private void UpdatePageHeight(double viewportHeight)
    {
        if (viewportHeight > 0)
        {
            PageBorder.Height = viewportHeight;
        }
    }

    private void UpdateHeaderLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        bool narrow = width < NarrowContentWidth;
        if (narrow == _isNarrow)
        {
            return;
        }

        _isNarrow = narrow;
        if (narrow)
        {
            HeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
            HeaderGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetRow(HeaderActions, 1);
            Grid.SetColumn(HeaderActions, 0);
            HeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
            HeaderActions.Margin = new Thickness(0, 12, 0, 0);
            return;
        }

        HeaderGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
        HeaderGrid.RowDefinitions = new RowDefinitions("Auto");
        Grid.SetRow(HeaderActions, 0);
        Grid.SetColumn(HeaderActions, 1);
        HeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
        HeaderActions.Margin = new Thickness(0);
    }

    private async void OnListenToggled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WeChatVideoViewModel viewModel ||
            ListenSwitch.IsChecked == viewModel.IsListening)
        {
            return;
        }

        try
        {
            await viewModel.SetListeningAsync(ListenSwitch.IsChecked == true);
        }
        catch (Exception exception)
        {
            viewModel.ReportPageFailure(exception);
        }
    }

    private async void OnChooseDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WeChatVideoViewModel viewModel)
        {
            return;
        }

        try
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var options = new FolderPickerOpenOptions { AllowMultiple = false };
            await PickerStartLocation.ApplyAsync(
                options,
                topLevel,
                viewModel.ReportPickerFailure,
                viewModel.DownloadDirectory);

            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            {
                viewModel.SetDownloadDirectory(path);
            }
        }
        catch (Exception exception)
        {
            viewModel.ReportPickerFailure(exception);
        }
    }
}
