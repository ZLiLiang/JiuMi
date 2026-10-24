using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using JiuMi.Application.Errors;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The single window for failures: it copies the details and then exits or continues.
/// </summary>
public partial class ErrorWindow : Window
{
    private const int FailureExitCode = 1;

    private Action<int>? _requestExit;
    private Action<Exception>? _reportCopyFailure;
    private bool _completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorWindow"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader; call <see cref="ShowFailure"/> to display it.</remarks>
    public ErrorWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    /// <summary>
    /// Shows a failure.
    /// </summary>
    /// <param name="request">The failure to show.</param>
    /// <param name="requestExit">Leaves the process with the given exit code.</param>
    /// <param name="reportCopyFailure">Records a clipboard failure.</param>
    public void ShowFailure(
        ErrorWindowRequest request,
        Action<int> requestExit,
        Action<Exception> reportCopyFailure)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestExit);
        ArgumentNullException.ThrowIfNull(reportCopyFailure);

        DataContext = new ErrorWindowViewModel(request);
        _requestExit = requestExit;
        _reportCopyFailure = reportCopyFailure;

        // The window uses the Fluent default theme of the operating system, never a user theme choice.
        PlatformThemeVariant? systemTheme =
            Avalonia.Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant;
        RequestedThemeVariant = systemTheme switch
        {
            PlatformThemeVariant.Light => ThemeVariant.Light,
            PlatformThemeVariant.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        Show();
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Button primary = DataContext is ErrorWindowViewModel { CanContinue: true } ? ContinueButton : ExitButton;
        Dispatcher.UIThread.Post(() => primary.Focus());
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // The title bar close button follows the variant's default action rather than just dismissing the window.
        if (_completed || DataContext is not ErrorWindowViewModel viewModel)
        {
            return;
        }

        e.Cancel = true;
        Complete(viewModel.DefaultAction);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The clipboard is owned by the desktop; a failed copy must not crash the error window.")]
    private async void OnCopyDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ErrorWindowViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(viewModel.DetailsText);
            viewModel.NotifyCopied();
        }
        catch (Exception exception)
        {
            // Another process can hold the clipboard; record it and leave the details readable.
            _reportCopyFailure?.Invoke(exception);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Complete(ErrorWindowAction.Exit);

    private void OnContinueClick(object? sender, RoutedEventArgs e) => Complete(ErrorWindowAction.Continue);

    private void Complete(ErrorWindowAction action)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        if (action == ErrorWindowAction.Exit)
        {
            if (_requestExit is null)
            {
                Close();
                return;
            }

            _requestExit(FailureExitCode);
            return;
        }

        Dispatcher.UIThread.Post(Close);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        (DataContext as ErrorWindowViewModel)?.Dispose();
    }
}
