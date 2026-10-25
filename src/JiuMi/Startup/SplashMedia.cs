using Avalonia.Labs.Gif;
using Avalonia.Media.Imaging;

namespace JiuMi.Startup;

/// <summary>
/// The media the splash shows, together with how long one pass of it takes. Which rung of the
/// resource ladder it came from is answered by what survived: the animation, then the poster, then
/// nothing but the background.
/// </summary>
/// <param name="Duration">The length of a single animation pass, or zero where there is none.</param>
/// <param name="Animation">The decoded GIF, when the animation rung survived.</param>
/// <param name="Poster">The still image, when the poster rung survived.</param>
internal sealed record SplashMedia(
    TimeSpan Duration,
    GifStreamSource? Animation,
    Bitmap? Poster) : IDisposable
{
    /// <summary>
    /// Releases the media once nothing draws it any more.
    /// </summary>
    public void Dispose()
    {
        Animation?.Dispose();
        Poster?.Dispose();
    }
}
