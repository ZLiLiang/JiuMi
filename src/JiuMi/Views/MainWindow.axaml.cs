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
    private readonly Serilog.ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>Required by the Avalonia runtime XAML loader.</remarks>
    public MainWindow()
    {
        InitializeComponent();
        TitleBar.Height = 40;
        TitleBar.ExtendsContentIntoTitleBar = true;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        Activated += OnFirstActivated;
    }

    private void OnFirstActivated(object? sender, EventArgs e)
    {
        Activated -= OnFirstActivated;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.InfoBar.Activate();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class with its view model.
    /// </summary>
    /// <param name="viewModel">The shell view model.</param>
    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class with its view model and logger.
    /// </summary>
    /// <param name="viewModel">The shell view model.</param>
    /// <param name="logger">Receives the window show and close events.</param>
    public MainWindow(MainWindowViewModel viewModel, Serilog.ILogger logger)
        : this(viewModel)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateBackdrop();
        _logger?.Information("Shell main window shown");
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _logger?.Information("Shell main window closed");
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
            return;
        }

        // A themed lookup must name the variant, otherwise it returns the default (light) brush.
        if (ResourceNodeExtensions.TryFindResource(
                this,
                ThemeResourceKeys.SolidBackgroundBrush,
                ActualThemeVariant,
                out object? solidBrush) &&
            solidBrush is IBrush brush)
        {
            Background = brush;
        }
    }
}
