using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// Plays the shell's content transitions: the home tool card growing into the page area (550 ms) and
/// the cross-fade used by the navigation rail (200 ms). The page swap happens inside the transition.
/// </summary>
internal sealed class PageTransitionAnimator
{
    private static readonly TimeSpan _cardGrowthDuration = TimeSpan.FromMilliseconds(413);
    private static readonly TimeSpan _cardFadeDuration = TimeSpan.FromMilliseconds(137);
    private static readonly TimeSpan _navigationFadeInDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan _navigationFadeOutDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(16);

    private const double CardStartCornerRadius = 8;
    private const double CardEndCornerRadius = 12;
    private const double CardBadgeInset = 18;
    private const double CardCommitProgress = 0.9;
    private const double NavigationPageOffset = 8;
    private const string CardStrokeBrushKey = "CardStrokeColorDefaultBrush";
    private const string FlySurfaceBrushKey = "JiuMiFlySurfaceBrush";

    private readonly MainWindow _window;
    private readonly ScrollViewer _scroller;
    private readonly ContentControl _pageHost;
    private readonly Canvas _layer;
    private readonly MainWindowViewModel _viewModel;
    private readonly Func<bool> _isMotionEnabled;
    private readonly DispatcherTimer _timer;
    private CardRun? _card;
    private NavigationRun? _navigation;
    private readonly HashSet<ShellPage> _warmedPages = [];
    private bool _detached;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageTransitionAnimator"/> class.
    /// </summary>
    /// <param name="window">The shell window, used for theme resources and the render scale.</param>
    /// <param name="scroller">The scroll viewer that holds the current page.</param>
    /// <param name="pageHost">The content control whose content is swapped.</param>
    /// <param name="layer">The overlay covering the page viewport.</param>
    /// <param name="viewModel">The shell view model that owns the current page.</param>
    /// <param name="isMotionEnabled">Reports whether the operating system asks for animations.</param>
    public PageTransitionAnimator(
        MainWindow window,
        ScrollViewer scroller,
        ContentControl pageHost,
        Canvas layer,
        MainWindowViewModel viewModel,
        Func<bool> isMotionEnabled)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(scroller);
        ArgumentNullException.ThrowIfNull(pageHost);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(isMotionEnabled);

        _window = window;
        _scroller = scroller;
        _pageHost = pageHost;
        _layer = layer;
        _viewModel = viewModel;
        _isMotionEnabled = isMotionEnabled;
        _timer = new DispatcherTimer(_frameInterval, DispatcherPriority.Render, OnFrame);
        _viewModel.PageNavigationRequested += OnPageNavigationRequested;
    }

    /// <summary>
    /// Jumps running transitions to their end state, used when the window is resized.
    /// </summary>
    public void FinishImmediately()
    {
        if (_card is not null)
        {
            FinishCard();
        }

        if (_navigation is not null)
        {
            FinishNavigation();
        }
    }

    /// <summary>
    /// Ends a running card entry when its own navigation item is pressed again; the highlight is already
    /// on that item, so no selection change follows to interrupt the animation.
    /// </summary>
    /// <param name="page">The page of the navigation item that was pressed, if any.</param>
    public void FinishCardFor(ShellPage? page)
    {
        if (_card is { } card && page == card.Target)
        {
            FinishCard();
        }
    }

    /// <summary>
    /// Detaches from the view model and releases transition resources as the window closes.
    /// </summary>
    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _viewModel.PageNavigationRequested -= OnPageNavigationRequested;
        _timer.Stop();

        if (_card is { } card)
        {
            CleanupCard(card);
        }

        if (_navigation is { } navigation)
        {
            CleanupNavigation(navigation);
        }
    }

    /// <summary>
    /// Builds and lays out a page view once so its first appearance does not stall the transition that
    /// reveals it. The warmed view is discarded; the template and style caches keep the work.
    /// </summary>
    /// <param name="page">The page to warm.</param>
    public void WarmPage(ShellPage page)
    {
        if (!_warmedPages.Add(page))
        {
            return;
        }

        object target = _viewModel.GetPage(page);
        IDataTemplate? template = Avalonia.Application.Current?.DataTemplates
            .FirstOrDefault(candidate => candidate.Match(target));
        if (template?.Build(target) is not Control view)
        {
            return;
        }

        view.DataContext = target;
        view.Measure(new Size(_scroller.Bounds.Width, double.PositiveInfinity));
        view.Arrange(new Rect(default, view.DesiredSize));
    }

    private void OnPageNavigationRequested(object? sender, PageNavigationRequestedEventArgs e)
    {
        if (e.FromCard)
        {
            if (_card is not null)
            {
                e.Accepted = false;
                return;
            }

            if (_navigation is not null)
            {
                AbortNavigation();
            }

            e.Handled = true;
            if (!_isMotionEnabled() || !TryStartCard(e.Page))
            {
                CommitAndFocus(e.Page);
            }

            return;
        }

        e.Handled = true;

        if (_card is not null)
        {
            AbortCard();
            CommitAndFocus(e.Page);
            return;
        }

        if (_navigation is not null)
        {
            AbortNavigation();
        }

        if (_viewModel.IsPageShown(e.Page))
        {
            return;
        }

        if (!_isMotionEnabled() || !TryStartNavigation(e.Page))
        {
            CommitAndFocus(e.Page);
        }
    }

    private bool TryStartCard(ShellPage page)
    {
        if (_pageHost.Content is not HomeViewModel)
        {
            return false;
        }

        WarmPage(page);

        HomeView? home = _pageHost.GetVisualDescendants().OfType<HomeView>().FirstOrDefault();
        if (home is null ||
            !TryGetCardControls(home, page, out Button? card, out Border? badge) ||
            card is null ||
            badge is null)
        {
            return false;
        }

        Point? origin = card.TranslatePoint(default, _layer);
        Size viewport = _layer.Bounds.Size;
        Size cardSize = card.Bounds.Size;
        if (origin is null || viewport.Width <= 0 || viewport.Height <= 0 ||
            cardSize.Width <= 0 || cardSize.Height <= 0)
        {
            return false;
        }

        RenderTargetBitmap? badgeBitmap = CaptureBadge(badge);
        try
        {
            if (badgeBitmap is null)
            {
                return false;
            }

            var badgeImage = new Image
            {
                Source = badgeBitmap,
                Width = badge.Bounds.Width,
                Height = badge.Bounds.Height,
                Stretch = Stretch.Fill,
                Margin = new Thickness(CardBadgeInset),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            var proxy = new Border
            {
                Background = FindBrush(FlySurfaceBrushKey) ?? Brushes.Transparent,
                BorderBrush = FindBrush(CardStrokeBrushKey),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CardStartCornerRadius),
                ClipToBounds = true,
                Child = badgeImage,
            };

            var start = new Rect(origin.Value, cardSize);
            var end = new Rect(default, viewport);
            var run = new CardRun
            {
                Target = page,
                Start = start,
                End = end,
                Proxy = proxy,
                BadgeBitmap = badgeBitmap,
                SourceCard = card,
            };
            badgeBitmap = null;

            Canvas.SetLeft(proxy, start.X);
            Canvas.SetTop(proxy, start.Y);
            proxy.Width = start.Width;
            proxy.Height = start.Height;
            _layer.Children.Add(proxy);

            card.Opacity = 0;
            card.IsHitTestVisible = false;

            _card = run;
            ApplyCardFrame(run, 0);
            _timer.Start();
            return true;
        }
        finally
        {
            badgeBitmap?.Dispose();
        }
    }

    private bool TryStartNavigation(ShellPage page)
    {
        WarmPage(page);

        ViewportSnapshot? snapshot = CaptureViewport();
        if (snapshot is null)
        {
            return false;
        }

        _viewModel.CommitPage(page);
        _scroller.Offset = default;

        var offset = new TranslateTransform(0, NavigationPageOffset);
        var run = new NavigationRun
        {
            Snapshot = snapshot.Image,
            SnapshotBitmap = snapshot.Bitmap,
            PageOffset = offset,
        };

        _layer.Children.Add(snapshot.Image);
        _pageHost.RenderTransform = offset;
        _navigation = run;
        ApplyNavigationFrame(run, 0);
        _timer.Start();
        return true;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_card is { } card)
        {
            double elapsed = card.Clock.Elapsed.TotalMilliseconds;
            double growth = _cardGrowthDuration.TotalMilliseconds;
            double fade = _cardFadeDuration.TotalMilliseconds;

            // The page swap builds and lays out the next page, so it happens while the proxy already
            // covers the viewport (the last fraction of growth is sub-pixel), then the fade keeps its
            // full duration instead of being swallowed by the layout pass.
            if (!card.PageCommitted && elapsed >= growth * CardCommitProgress)
            {
                ApplyCardFrame(card, Math.Min(1, elapsed / growth));
                _viewModel.CommitPage(card.Target);
                _scroller.Offset = default;
                card.PageCommitted = true;
                card.FadeStartedAt = Math.Max(growth, card.Clock.Elapsed.TotalMilliseconds);
                _pageHost.Opacity = 0;
                elapsed = card.Clock.Elapsed.TotalMilliseconds;
            }

            if (!card.PageCommitted)
            {
                ApplyCardFrame(card, elapsed / growth);
                return;
            }

            if (elapsed < card.FadeStartedAt)
            {
                ApplyCardFrame(card, Math.Min(1, elapsed / growth));
                return;
            }

            double fadeProgress = Math.Clamp(
                (elapsed - card.FadeStartedAt) / fade,
                0,
                1);
            card.Proxy.Opacity = 1 - fadeProgress;
            _pageHost.Opacity = fadeProgress;

            if (fadeProgress >= 1)
            {
                FinishCard();
            }

            return;
        }

        if (_navigation is { } navigation)
        {
            double elapsed = Math.Min(
                navigation.Clock.Elapsed.TotalMilliseconds,
                _navigationFadeInDuration.TotalMilliseconds);
            ApplyNavigationFrame(navigation, elapsed);

            if (elapsed >= _navigationFadeInDuration.TotalMilliseconds)
            {
                FinishNavigation();
            }
        }
    }

    private static void ApplyCardFrame(CardRun run, double progress)
    {
        double eased = EaseOutCubic(progress);
        double left = run.Start.X + ((run.End.X - run.Start.X) * eased);
        double top = run.Start.Y + ((run.End.Y - run.Start.Y) * eased);
        double width = run.Start.Width + ((run.End.Width - run.Start.Width) * eased);
        double height = run.Start.Height + ((run.End.Height - run.Start.Height) * eased);

        Canvas.SetLeft(run.Proxy, left);
        Canvas.SetTop(run.Proxy, top);
        run.Proxy.Width = width;
        run.Proxy.Height = height;
        run.Proxy.CornerRadius = new CornerRadius(
            CardStartCornerRadius + ((CardEndCornerRadius - CardStartCornerRadius) * eased));
    }

    private void ApplyNavigationFrame(NavigationRun run, double elapsedMilliseconds)
    {
        double fadeIn = EaseOutCubic(elapsedMilliseconds / _navigationFadeInDuration.TotalMilliseconds);
        _pageHost.Opacity = fadeIn;
        run.PageOffset.Y = NavigationPageOffset * (1 - fadeIn);
        run.Snapshot.Opacity = Math.Clamp(
            1 - (elapsedMilliseconds / _navigationFadeOutDuration.TotalMilliseconds),
            0,
            1);
    }

    private void FinishCard()
    {
        if (_card is not { } card)
        {
            return;
        }

        _card = null;
        _timer.Stop();

        if (!card.PageCommitted)
        {
            _viewModel.CommitPage(card.Target);
            _scroller.Offset = default;
        }

        CleanupCard(card);
        FocusFirstFocusable();
    }

    private void AbortCard()
    {
        if (_card is not { } card)
        {
            return;
        }

        _card = null;
        _timer.Stop();
        CleanupCard(card);
    }

    private void CleanupCard(CardRun card)
    {
        _layer.Children.Remove(card.Proxy);
        card.BadgeBitmap.Dispose();
        card.SourceCard.Opacity = 1;
        card.SourceCard.IsHitTestVisible = true;
        _pageHost.Opacity = 1;
    }

    private void FinishNavigation()
    {
        if (_navigation is not { } navigation)
        {
            return;
        }

        _navigation = null;
        _timer.Stop();
        CleanupNavigation(navigation);
        FocusFirstFocusable();
    }

    private void AbortNavigation()
    {
        if (_navigation is not { } navigation)
        {
            return;
        }

        _navigation = null;
        _timer.Stop();
        CleanupNavigation(navigation);
    }

    private void CleanupNavigation(NavigationRun navigation)
    {
        _layer.Children.Remove(navigation.Snapshot);
        navigation.SnapshotBitmap.Dispose();
        _pageHost.Opacity = 1;
        _pageHost.RenderTransform = null;
    }

    private void CommitAndFocus(ShellPage page)
    {
        _viewModel.CommitPage(page);
        _scroller.Offset = default;
        Dispatcher.UIThread.Post(FocusFirstFocusable, DispatcherPriority.Background);
    }

    private void FocusFirstFocusable()
    {
        Control? target = _pageHost
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(static control =>
                control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);

        target?.Focus();
    }

    private ViewportSnapshot? CaptureViewport()
    {
        Size viewport = _scroller.Bounds.Size;
        RenderTargetBitmap? bitmap = Capture(_scroller, viewport);
        try
        {
            if (bitmap is null)
            {
                return null;
            }

            var image = new Image
            {
                Source = bitmap,
                Width = viewport.Width,
                Height = viewport.Height,
                Stretch = Stretch.Fill,
            };

            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            var snapshot = new ViewportSnapshot(image, bitmap);
            bitmap = null;
            return snapshot;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private RenderTargetBitmap? CaptureBadge(Border badge) => Capture(badge, badge.Bounds.Size);

    private RenderTargetBitmap? Capture(Visual visual, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }

        double scale = TopLevel.GetTopLevel(_window)?.RenderScaling ?? 1;
        var pixelSize = new PixelSize(
            (int)Math.Ceiling(size.Width * scale),
            (int)Math.Ceiling(size.Height * scale));
        var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96 * scale, 96 * scale));
        bitmap.Render(visual);
        return bitmap;
    }

    private IBrush? FindBrush(string key)
    {
        if (ResourceNodeExtensions.TryFindResource(_window, key, _window.ActualThemeVariant, out object? value) &&
            value is IBrush brush)
        {
            return brush;
        }

        return null;
    }

    private static bool TryGetCardControls(
        HomeView home,
        ShellPage page,
        out Button? card,
        out Border? badge)
    {
        (string cardName, string badgeName) = page switch
        {
            ShellPage.FileRename => ("FileRenameCard", "FileRenameBadge"),
            ShellPage.ImageConvert => ("ImageConvertCard", "ImageConvertBadge"),
            ShellPage.WorldClock => ("WorldClockCard", "WorldClockBadge"),
            _ => (string.Empty, string.Empty),
        };

        card = cardName.Length == 0 ? null : home.FindControl<Button>(cardName);
        badge = badgeName.Length == 0 ? null : home.FindControl<Border>(badgeName);
        return card is not null && badge is not null;
    }

    private static double EaseOutCubic(double progress)
    {
        double remaining = 1 - Math.Clamp(progress, 0, 1);
        return 1 - (remaining * remaining * remaining);
    }

    private sealed class CardRun
    {
        public required ShellPage Target
        {
            get;
            init;
        }

        public required Rect Start
        {
            get;
            init;
        }

        public required Rect End
        {
            get;
            init;
        }

        public required Border Proxy
        {
            get;
            init;
        }

        public required RenderTargetBitmap BadgeBitmap
        {
            get;
            init;
        }

        public required Control SourceCard
        {
            get;
            init;
        }

        public bool PageCommitted
        {
            get;
            set;
        }

        public double FadeStartedAt
        {
            get;
            set;
        }

        public Stopwatch Clock
        {
            get;
        } = Stopwatch.StartNew();
    }

    private sealed class NavigationRun
    {
        public required Image Snapshot
        {
            get;
            init;
        }

        public required RenderTargetBitmap SnapshotBitmap
        {
            get;
            init;
        }

        public required TranslateTransform PageOffset
        {
            get;
            init;
        }

        public Stopwatch Clock
        {
            get;
        } = Stopwatch.StartNew();
    }

    private sealed class ViewportSnapshot
    {
        public ViewportSnapshot(Image image, RenderTargetBitmap bitmap)
        {
            Image = image;
            Bitmap = bitmap;
        }

        public Image Image
        {
            get;
        }

        public RenderTargetBitmap Bitmap
        {
            get;
        }
    }
}
