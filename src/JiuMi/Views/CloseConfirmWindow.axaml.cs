using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// Asks whether closing the main window should exit or hide to the tray.
/// </summary>
public partial class CloseConfirmWindow : Window
{
    private bool _completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloseConfirmWindow"/> class.
    /// </summary>
    public CloseConfirmWindow()
    {
        InitializeComponent();
        Icon = AppIcon.CreateWindowIcon();
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(() => MinimizeButton.Focus());
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_completed)
        {
            base.OnClosing(e);
            return;
        }

        // The title bar close button is another way to cancel.
        e.Cancel = true;
        Complete(CloseConfirmAction.Cancel);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Complete(CloseConfirmAction.Cancel);

    private void OnExitClick(object? sender, RoutedEventArgs e) => Complete(CloseConfirmAction.Exit);

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) =>
        Complete(CloseConfirmAction.MinimizeToTray);

    private void Complete(CloseConfirmAction action)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        bool dontAsk = DataContext is CloseConfirmWindowViewModel { DontAsk: true };
        Dispatcher.UIThread.Post(() => Close(new CloseConfirmResult(action, dontAsk)));
    }
}

/// <summary>
/// The action the close confirmation returned.
/// </summary>
public enum CloseConfirmAction
{
    /// <summary>Keep the window open.</summary>
    Cancel,

    /// <summary>Exit the process.</summary>
    Exit,

    /// <summary>Hide the window to the tray.</summary>
    MinimizeToTray,
}

/// <summary>
/// The outcome of the close confirmation.
/// </summary>
/// <param name="Action">The chosen action.</param>
/// <param name="DontAsk">Whether the choice should be remembered.</param>
public sealed record CloseConfirmResult(CloseConfirmAction Action, bool DontAsk);
