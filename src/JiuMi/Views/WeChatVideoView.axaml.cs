using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The WeChat video download page: the folder picker, the listener switch and the responsive header.
/// </summary>
public partial class WeChatVideoView : UserControl
{
    private const double NarrowContentWidth = 640;
    private const double TitleBarAndIslandHeight = 68;

    private TopLevel? _topLevel;
    private ScrollViewer? _pageScrollViewer;
    private bool _isNarrow;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoView"/> class.
    /// </summary>
    public WeChatVideoView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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

        UpdateHeaderLayout(PageContent.Bounds.Width);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        PageContent.SizeChanged -= OnPageContentSizeChanged;
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

    private void OnPageContentSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateHeaderLayout(e.NewSize.Width);

    private void OnPageScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdatePageHeight(e.NewSize.Height);

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_pageScrollViewer is null)
        {
            UpdatePageHeight(e.NewSize.Height - TitleBarAndIslandHeight);
        }
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

    private async void OnScanPreloadCacheClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WeChatVideoViewModel viewModel)
        {
            return;
        }

        await viewModel.ScanPreloadCacheAsync();
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
