using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Windowing;
using JiuMi.Theming;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The application shell window.
/// </summary>
public partial class MainWindow : FAAppWindow
{
    private readonly Serilog.ILogger _logger = Serilog.Core.Logger.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader.</remarks>
    public MainWindow()
    {
        InitializeComponent();
        ApplyBrandMark();
        TitleBar.Height = 40;
        TitleBar.ExtendsContentIntoTitleBar = true;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        Activated += OnFirstActivated;
        Activated += OnWindowActivated;
    }

    private void ApplyBrandMark()
    {
        // FAAppWindow wraps any value assigned to Icon in a WindowIcon, so a missing asset must leave
        // the property untouched instead of clearing it.
        if (AppIcon.Mark is not { } mark)
        {
            return;
        }

        Icon = mark;
        TitleBarIcon.Source = mark;

        // The mark carries its own rounded corners; a gradient behind it would show through them.
        TitleBarMark.Background = null;
    }

    private void OnFirstActivated(object? sender, EventArgs e)
    {
        Activated -= OnFirstActivated;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.InfoBar.Activate();
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OnWindowActivated();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class with its view model and logger.
    /// </summary>
    /// <param name="viewModel">The shell view model.</param>
    /// <param name="logger">Receives the window show and close events.</param>
    public MainWindow(MainWindowViewModel viewModel, Serilog.ILogger logger)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(logger);
        DataContext = viewModel;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateBackdrop();
        _logger.Information("Shell main window shown");
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _logger.Information("Shell main window closed");
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        base.OnClosed(e);
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveOnExit();
        }

        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActualTransparencyLevelProperty)
        {
            UpdateBackdrop();
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => UpdateBackdrop();

    private void UpdateBackdrop()
    {
        if (ActualTransparencyLevel == WindowTransparencyLevel.Mica)
        {
            Background = null;
            BackdropHighlight.IsVisible = false;
            return;
        }

        // A themed lookup must name the variant, otherwise it returns the default (light) brush.
        bool hasBackdrop = TryFindBrush(ThemeResourceKeys.WindowBackdropBrush, out IBrush? backdrop);
        BackdropHighlight.IsVisible = hasBackdrop;

        if (hasBackdrop)
        {
            Background = backdrop;
            return;
        }

        if (TryFindBrush(ThemeResourceKeys.SolidBackgroundBrush, out IBrush? solid))
        {
            Background = solid;
        }
    }

    private bool TryFindBrush(string key, out IBrush? brush)
    {
        if (ResourceNodeExtensions.TryFindResource(this, key, ActualThemeVariant, out object? value) &&
            value is IBrush found)
        {
            brush = found;
            return true;
        }

        brush = null;
        return false;
    }
}
