using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The image conversion page: file pickers, drag and drop, and the drop overlay.
/// </summary>
public partial class ImageConvertView : UserControl
{
    private const double ListChromeHeight = 420;
    private const double AdvancedPanelHeight = 132;
    private const double MinListHeight = 120;

    private static readonly FilePickerFileType _imageFileType = new("Images")
    {
        Patterns =
        [
            "*.psd",
            "*.psb",
            "*.png",
            "*.jpg",
            "*.jpeg",
            "*.webp",
            "*.bmp",
        ],
    };

    private TopLevel? _topLevel;
    private ImageConvertViewModel? _viewModel;
    private bool _advancedExpanded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageConvertView"/> class.
    /// </summary>
    public ImageConvertView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.SizeChanged += OnTopLevelSizeChanged;
            UpdateListHeight(_topLevel.ClientSize.Height);
        }

        if (DataContext is ImageConvertViewModel viewModel)
        {
            _viewModel = viewModel;
            _advancedExpanded = viewModel.IsAdvancedExpanded;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.SizeChanged -= OnTopLevelSizeChanged;
            _topLevel = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImageConvertViewModel.IsAdvancedExpanded) ||
            _viewModel is not { } viewModel)
        {
            return;
        }

        _advancedExpanded = viewModel.IsAdvancedExpanded;
        if (_topLevel is not null)
        {
            UpdateListHeight(_topLevel.ClientSize.Height);
        }
    }

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateListHeight(e.NewSize.Height);

    private void UpdateListHeight(double windowHeight)
    {
        double reserved = ListChromeHeight + (_advancedExpanded ? AdvancedPanelHeight : 0);
        FileListScroll.MaxHeight = Math.Max(MinListHeight, windowHeight - reserved);
    }

    private void OnDragEnter(object? sender, DragEventArgs e) => UpdateDragState(e);

    private void OnDragOver(object? sender, DragEventArgs e) => UpdateDragState(e);

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        e.Handled = true;
        if (DataContext is not ImageConvertViewModel viewModel || e.DataTransfer is null)
        {
            return;
        }

        try
        {
            IStorageItem[]? items = e.DataTransfer.TryGetFiles();
            if (items is null || items.Length == 0)
            {
                return;
            }

            var paths = new List<string>(items.Length);
            foreach (IStorageItem item in items)
            {
                if (item.TryGetLocalPath() is { } path)
                {
                    paths.Add(path);
                }
            }

            // The drag source can still hold the files while the OLE drag loop is unwinding.
            // Queueing after input lets it release them before the probes read the files.
            Dispatcher.UIThread.Post(
                () => QueueDroppedFiles(viewModel, paths),
                DispatcherPriority.ApplicationIdle);
        }
        catch (Exception exception)
        {
            viewModel.ReportPickerFailure(exception);
        }
    }

    private static void QueueDroppedFiles(ImageConvertViewModel viewModel, IReadOnlyList<string> paths) =>
        _ = AddDroppedFilesAsync(viewModel, paths);

    private static async Task AddDroppedFilesAsync(
        ImageConvertViewModel viewModel,
        IReadOnlyList<string> paths)
    {
        try
        {
            await viewModel.AddFilesAsync(paths);
        }
        catch (Exception exception)
        {
            viewModel.ReportPickerFailure(exception);
        }
    }

    private async void OnAddFilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImageConvertViewModel viewModel)
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

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = true,
                    FileTypeFilter = [_imageFileType],
                });

            var paths = new List<string>(files.Count);
            foreach (IStorageFile file in files)
            {
                if (file.TryGetLocalPath() is { } path)
                {
                    paths.Add(path);
                }
            }

            await viewModel.AddFilesAsync(paths);
        }
        catch (Exception exception)
        {
            viewModel.ReportPickerFailure(exception);
        }
    }

    private async void OnChooseOutputDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ImageConvertViewModel viewModel)
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

            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { AllowMultiple = false });

            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            {
                viewModel.SetOutputDirectory(path);
            }
        }
        catch (Exception exception)
        {
            viewModel.ReportPickerFailure(exception);
        }
    }

    private void UpdateDragState(DragEventArgs e)
    {
        bool hasFiles = e.DataTransfer?.TryGetFiles() is { Length: > 0 };
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.IsVisible = hasFiles;
        e.Handled = true;
    }
}
