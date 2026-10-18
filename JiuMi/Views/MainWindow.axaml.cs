using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Windowing;
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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActualTransparencyLevelProperty)
        {
            UpdateBackdrop();
        }
    }

    private void UpdateBackdrop()
    {
        if (ActualTransparencyLevel == WindowTransparencyLevel.Mica)
        {
            Background = null;
            return;
        }

        if (this.TryFindResource("SolidBackgroundFillColorBaseBrush", out object? solidBrush) &&
            solidBrush is IBrush brush)
        {
            Background = brush;
        }
    }
}
