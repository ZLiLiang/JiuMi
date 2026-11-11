using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Windowing;
using JiuMi.Application;
using JiuMi.Theming;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The application shell window.
/// </summary>
public partial class MainWindow : FAAppWindow
{
    private static readonly ShellPage[] _preloadedPages =
    [
        ShellPage.MyJiuMi,
        ShellPage.FileRename,
        ShellPage.ImageConvert,
        ShellPage.WorldClock,
        ShellPage.Settings,
    ];

    private readonly Serilog.ILogger _logger = Serilog.Core.Logger.None;
    private readonly PageTransitionAnimator? _transitions;

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
        TitleBarMark.Background = Brushes.Transparent;
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
    /// <param name="animationSettings">The operating system preference for client-area animations.</param>
    /// <param name="logger">Receives the window show and close events.</param>
    public MainWindow(
        MainWindowViewModel viewModel,
        ISystemAnimationSettings animationSettings,
        Serilog.ILogger logger)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(animationSettings);
        ArgumentNullException.ThrowIfNull(logger);
        DataContext = viewModel;
        _logger = logger;
        _transitions = new PageTransitionAnimator(
            this,
            PageScroller,
            PageHost,
            TransitionLayer,
            viewModel,
            () => animationSettings.AreClientAreaAnimationsEnabled);
        SizeChanged += OnWindowSizeChanged;
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateBackdrop();
        _logger.Information("Shell main window shown");

        if (_transitions is not null)
        {
            Dispatcher.UIThread.Post(WarmPageViews, DispatcherPriority.Background);
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _logger.Information("Shell main window closed");
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        SizeChanged -= OnWindowSizeChanged;
        _transitions?.Detach();
        base.OnClosed(e);
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

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e) =>
        _transitions?.FinishImmediately();

    private void WarmPageViews()
    {
        foreach (ShellPage page in _preloadedPages)
        {
            _transitions?.WarmPage(page);
        }
    }

    private void OnNavigationPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ListBoxItem? item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is ShellNavigationItem navigationItem)
        {
            _transitions?.FinishCardFor(navigationItem.Page);
        }
    }

    private void UpdateBackdrop()
    {
        // TransparencyLevelHint is a XAML attribute on the root, so setting it raises
        // ActualTransparencyLevel while the loader is still building the content tree. On Windows 11
        // the platform accepts Mica at that point and this runs before BackdropHighlight exists.
        // OnOpened re-applies the backdrop once the named content is available.
        if (BackdropHighlight is null)
        {
            return;
        }

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
