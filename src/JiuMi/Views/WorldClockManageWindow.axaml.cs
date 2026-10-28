using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Windowing;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The manage-places dialog: search, select, reorder and edit working hours.
/// </summary>
public partial class WorldClockManageWindow : FAAppWindow
{
    private static readonly DataFormat<WorldClockSelectedPlaceViewModel> _placeFormat =
        DataFormat.CreateInProcessFormat<WorldClockSelectedPlaceViewModel>("JiuMi.Place");

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldClockManageWindow"/> class.
    /// </summary>
    public WorldClockManageWindow()
    {
        InitializeComponent();
        TitleBar.Height = 40;
        TitleBar.ExtendsContentIntoTitleBar = true;

        if (AppIcon.Mark is { } mark)
        {
            Icon = mark;
            TitleBarIcon.Source = mark;
            TitleBarMark.Background = null;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is WorldClockManageViewModel { IsEditing: true } editing)
        {
            editing.CancelEditCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: WorldClockSelectedPlaceViewModel place })
        {
            return;
        }

        try
        {
            using var transfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(_placeFormat, place);
            transfer.Add(item);
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
        }
        catch (Exception exception) when (DataContext is WorldClockManageViewModel viewModel)
        {
            viewModel.ReportDragFailure(exception);
        }
    }

    private void OnSelectedDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(_placeFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void OnSelectedDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(_placeFormat) is { } from &&
            sender is Control { DataContext: WorldClockSelectedPlaceViewModel target } &&
            DataContext is WorldClockManageViewModel viewModel)
        {
            viewModel.Move(from.Index, target.Index);
        }
    }
}
