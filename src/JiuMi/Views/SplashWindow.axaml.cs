using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Gif;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using JiuMi.Theming;

namespace JiuMi.Views;

/// <summary>
/// The borderless startup window: it plays the splash animation while the shell is initialised.
/// </summary>
public partial class SplashWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplashWindow"/> class.
    /// </summary>
    public SplashWindow()
    {
        InitializeComponent();
        LockBackground();
    }

    /// <summary>
    /// Shows the animation and takes the ring away.
    /// </summary>
    /// <param name="source">The decoded GIF to play.</param>
    public void ShowAnimation(IGifSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        MediaGif.Source = source;
        MediaGif.IsVisible = true;
        MediaRing.IsVisible = false;
    }

    /// <summary>
    /// Shows the static poster with the ring, for the rung where the animation is missing.
    /// </summary>
    /// <param name="poster">The still image to draw.</param>
    public void ShowPoster(Bitmap poster)
    {
        ArgumentNullException.ThrowIfNull(poster);

        MediaPoster.Source = poster;
        MediaPoster.IsVisible = true;
        MediaRing.IsVisible = true;
    }

    /// <summary>
    /// Shows the locked background and the ring, for the rung where no media survived.
    /// </summary>
    public void ShowBackgroundOnly() => MediaRing.IsVisible = true;

    /// <summary>
    /// Stops drawing the animation and puts the ring up, in the same frame.
    /// </summary>
    public void StopAnimation()
    {
        // Clearing the source is what stops the control: it never stops by itself, and a finished
        // GIF keeps redrawing the frame it reached.
        MediaGif.Source = null;
        MediaRing.IsVisible = true;
    }

    private void LockBackground()
    {
        // The colour resolved here is copied into a plain solid brush on purpose. The user theme is
        // applied later in startup, and a themed reference would repaint the splash mid-animation.
        if (ResourceNodeExtensions.TryFindResource(
                this,
                ThemeResourceKeys.SolidBackgroundBrush,
                ActualThemeVariant,
                out object? resource) &&
            resource is SolidColorBrush solidBrush)
        {
            Background = new SolidColorBrush(solidBrush.Color);
        }
    }
}
