using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Gif;
using Avalonia.Platform;

namespace JiuMi.Views;

/// <summary>
/// The home page: introduction, hero media and the tool cards.
/// </summary>
public partial class HomeView : UserControl
{
    private const double NarrowContentWidth = 760;
    private const double HeroTextSpacing = 16;

    private static readonly Uri _heroMediaUri = new("avares://JiuMi/Assets/splash.gif");

    private GifStreamSource? _heroSource;
    private bool _isNarrow;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeView"/> class.
    /// </summary>
    public HomeView() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PageContent.PropertyChanged += OnPageContentPropertyChanged;
        LoadHeroMedia();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PageContent.PropertyChanged -= OnPageContentPropertyChanged;
        DisposeHeroMedia();
    }

    private void OnPageContentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != BoundsProperty)
        {
            return;
        }

        double width = PageContent.Bounds.Width;
        bool narrow = width > 0 && width < NarrowContentWidth;
        if (narrow == _isNarrow)
        {
            return;
        }

        _isNarrow = narrow;
        ApplyLayout(narrow);
    }

    private void LoadHeroMedia()
    {
        if (_heroSource is not null)
        {
            return;
        }

        if (!AssetLoader.Exists(_heroMediaUri))
        {
            ShowHeroPlaceholder();
            return;
        }

        try
        {
            _heroSource = GifStreamSource.FromUri(_heroMediaUri);
            HeroGif.Source = _heroSource;
            HeroGif.IsVisible = true;
            HeroPlaceholder.IsVisible = false;
        }
#pragma warning disable CA1031 // A broken decorative asset must degrade to the placeholder instead of failing the page.
        catch (Exception)
#pragma warning restore CA1031
        {
            ShowHeroPlaceholder();
        }
    }

    private void ShowHeroPlaceholder()
    {
        HeroGif.Source = null;
        HeroGif.IsVisible = false;
        HeroPlaceholder.IsVisible = true;
    }

    private void DisposeHeroMedia()
    {
        HeroGif.Source = null;
        _heroSource?.Dispose();
        _heroSource = null;
    }

    private void ApplyLayout(bool narrow)
    {
        if (narrow)
        {
            HeroLayout.ColumnDefinitions = new ColumnDefinitions("*");
            HeroLayout.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetRow(HeroMedia, 0);
            Grid.SetColumn(HeroMedia, 0);
            Grid.SetRow(HeroText, 1);
            Grid.SetColumn(HeroText, 0);
            HeroMedia.Width = double.NaN;
            HeroMedia.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            HeroText.Margin = new Thickness(0, HeroTextSpacing, 0, 0);
            HeroText.MaxWidth = double.PositiveInfinity;

            ToolGrid.ColumnDefinitions = new ColumnDefinitions("*");
            ToolGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
            PositionCard(WeChatCard, row: 0, column: 0);
            PositionCard(BatchRenameCard, row: 1, column: 0);
            PositionCard(IconConvertCard, row: 2, column: 0);
            PositionCard(WorldClockCard, row: 3, column: 0);
            return;
        }

        HeroLayout.ColumnDefinitions = new ColumnDefinitions("Auto,*");
        HeroLayout.RowDefinitions = new RowDefinitions("Auto");
        Grid.SetRow(HeroMedia, 0);
        Grid.SetColumn(HeroMedia, 0);
        Grid.SetRow(HeroText, 0);
        Grid.SetColumn(HeroText, 1);
        HeroMedia.Width = 340;
        HeroMedia.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        HeroText.Margin = new Thickness(0);
        HeroText.MaxWidth = 430;

        ToolGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
        ToolGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
        PositionCard(WeChatCard, row: 0, column: 0);
        PositionCard(BatchRenameCard, row: 0, column: 1);
        PositionCard(IconConvertCard, row: 1, column: 0);
        PositionCard(WorldClockCard, row: 1, column: 1);
    }

    private static void PositionCard(Control card, int row, int column)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
    }
}
