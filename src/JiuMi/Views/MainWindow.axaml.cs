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

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateBackdrop();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
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

        // A platform that rejects Mica leaves the window transparent; settle for a solid surface.
        if (ActualTransparencyLevel == WindowTransparencyLevel.Transparent &&
            TransparencyLevelHint.Contains(WindowTransparencyLevel.Mica))
        {
            TransparencyLevelHint = [WindowTransparencyLevel.None];
        }

        // A themed lookup must name the variant, otherwise it returns the default (light) brush.
        if (this.TryFindResource(
                ThemeResourceKeys.SolidBackgroundBrush,
                ActualThemeVariant,
                out object? solidBrush) &&
            solidBrush is IBrush brush)
        {
            Background = brush;
        }
    }
}
