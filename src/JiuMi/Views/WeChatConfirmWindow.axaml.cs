using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace JiuMi.Views;

/// <summary>
/// The borderless confirmation used for WeChat video certificate trust and removal.
/// </summary>
public partial class WeChatConfirmWindow : Window
{
    private bool _completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatConfirmWindow"/> class.
    /// </summary>
    public WeChatConfirmWindow()
    {
        InitializeComponent();
        Icon = AppIcon.CreateWindowIcon();
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(() => CancelButton.Focus());
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_completed)
        {
            base.OnClosing(e);
            return;
        }

        // The borderless card has no close button; Esc, Alt+F4 and the title-bar close all cancel.
        e.Cancel = true;
        Complete(false);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Complete(false);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Complete(true);

    private void Complete(bool result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Dispatcher.UIThread.Post(() => Close(result));
    }
}
