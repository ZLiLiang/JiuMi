using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The application-wide info bar shown at the top of the content area.
/// </summary>
public partial class InfoBarView : UserControl
{
    private const double NarrowContentWidth = 640;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfoBarView"/> class.
    /// </summary>
    public InfoBarView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        bool isNarrow = e.NewSize.Width < NarrowContentWidth;
        Grid.SetRow(ActionButton, isNarrow ? 1 : 0);
        Grid.SetColumn(ActionButton, isNarrow ? 1 : 2);
        Grid.SetColumnSpan(ActionButton, isNarrow ? 3 : 1);
        ActionButton.HorizontalAlignment = isNarrow
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        ActionButton.Margin = isNarrow ? new Thickness(0, 8, 0, 0) : default;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty) &&
            DataContext is InfoBarViewModel viewModel)
        {
            viewModel.SetInteractionPaused(IsPointerOver || IsKeyboardFocusWithin);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The clipboard is owned by the desktop; a failed copy must not crash the shell.")]
    private async void OnCopyDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InfoBarViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        string details = viewModel.DetailsText;
        if (string.IsNullOrEmpty(details))
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(details);
            viewModel.NotifyCopied();
        }
        catch (Exception exception)
        {
            // Another process can hold the clipboard; record it and let the user try again.
            viewModel.ReportCopyFailure(exception);
        }
    }
}
