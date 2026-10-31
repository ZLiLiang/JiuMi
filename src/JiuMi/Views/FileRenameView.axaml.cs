using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The batch rename page: the folder pickers, the confirmation dialog, drag and drop, and the
/// log save dialog. The rules and file work live in the view model and the application layer.
/// </summary>
public partial class FileRenameView : UserControl
{
    private const double TitleBarAndIslandHeight = 68;

    private TopLevel? _topLevel;
    private ScrollViewer? _pageScrollViewer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenameView"/> class.
    /// </summary>
    public FileRenameView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
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

    private async void OnPickFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FileRenameViewModel viewModel)
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
                await viewModel.SetDirectoryAsync(path);
            }
        }
        catch (Exception exception)
        {
            viewModel.ReportPageFailure(exception);
        }
    }

    private async void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FileRenameViewModel viewModel || !viewModel.CanRun)
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

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = viewModel.ConfirmBody,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            });
            content.Children.Add(new TextBlock
            {
                Text = viewModel.ConfirmNote,
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 11.5,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
            if (viewModel.ShowConfirmBackup)
            {
                content.Children.Add(new TextBlock
                {
                    Text = viewModel.ConfirmBackupText,
                    Margin = new Thickness(0, 4, 0, 0),
                    FontSize = 11.5,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            var dialog = new FAContentDialog
            {
                Title = viewModel.ConfirmTitle,
                Content = content,
                PrimaryButtonText = viewModel.RunActionText,
                CloseButtonText = viewModel.CancelText,
                DefaultButton = FAContentDialogButton.Close,
                MinWidth = 400,
            };

            FAContentDialogResult result = await dialog.ShowAsync(topLevel);
            if (result == FAContentDialogResult.Primary)
            {
                await viewModel.ExecuteAsync();
            }
        }
        catch (Exception exception)
        {
            viewModel.ReportPageFailure(exception);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FileRenameViewModel viewModel)
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

            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    SuggestedFileName = viewModel.DefaultExportFileName,
                    DefaultExtension = "json",
                    FileTypeChoices =
                    [
                        new FilePickerFileType(viewModel.ExportFileTypeName)
                        {
                            Patterns = ["*.json"],
                        },
                    ],
                });
            if (file is not null && file.TryGetLocalPath() is { } path)
            {
                await viewModel.ExportLogAsync(path);
            }
        }
        catch (Exception exception)
        {
            viewModel.ReportPageFailure(exception);
        }
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
        if (DataContext is not FileRenameViewModel viewModel || e.DataTransfer is null)
        {
            return;
        }

        try
        {
            IStorageItem[]? items = e.DataTransfer.TryGetFiles();
            if (items is null)
            {
                return;
            }

            string? directory = null;
            foreach (IStorageItem item in items)
            {
                if (item.TryGetLocalPath() is { } path && viewModel.IsDirectory(path))
                {
                    directory = path;
                    break;
                }
            }

            if (directory is null)
            {
                return;
            }

            Dispatcher.UIThread.Post(
                () => OpenDroppedFolder(viewModel, directory),
                DispatcherPriority.ApplicationIdle);
        }
        catch (Exception exception)
        {
            viewModel.ReportPageFailure(exception);
        }
    }

    private static void OpenDroppedFolder(FileRenameViewModel viewModel, string directory) =>
        _ = SetDroppedFolderAsync(viewModel, directory);

    private static async Task SetDroppedFolderAsync(FileRenameViewModel viewModel, string directory)
    {
        try
        {
            await viewModel.SetDirectoryAsync(directory);
        }
        catch (Exception exception)
        {
            viewModel.ReportPageFailure(exception);
        }
    }

    private void UpdateDragState(DragEventArgs e)
    {
        bool hasDirectory = false;
        if (DataContext is FileRenameViewModel viewModel &&
            e.DataTransfer?.TryGetFiles() is { Length: > 0 } items)
        {
            foreach (IStorageItem item in items)
            {
                if (item.TryGetLocalPath() is { } path && viewModel.IsDirectory(path))
                {
                    hasDirectory = true;
                    break;
                }
            }
        }

        e.DragEffects = hasDirectory ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.IsVisible = hasDirectory;
        e.Handled = true;
    }
}
