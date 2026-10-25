using System.IO;
using Avalonia.Labs.Gif;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using JiuMi.Application.Startup;

namespace JiuMi.Startup;

/// <summary>
/// Resolves the splash media by walking down the resource ladder until a rung has something to show.
/// </summary>
/// <remarks>
/// Dropping a rung only costs the animation: startup never waits for a missing asset, and the
/// downgrade is not reported to the user.
/// </remarks>
internal static class SplashMediaLoader
{
    private static readonly Uri _animationUri = new("avares://JiuMi/Assets/splash.gif");
    private static readonly Uri _posterUri = new("avares://JiuMi/Assets/splash.png");

    /// <summary>
    /// Reads the best media the packaged assets can give.
    /// </summary>
    /// <returns>The media to show, and how long the splash has to stay up for it.</returns>
    public static SplashMedia Load() =>
        TryLoadAnimation()
        ?? TryLoadPoster()
        ?? new SplashMedia(SplashMediaKind.BackgroundOnly, TimeSpan.Zero, null, null);

    private static SplashMedia? TryLoadAnimation()
    {
        if (!AssetLoader.Exists(_animationUri))
        {
            return null;
        }

        try
        {
            TimeSpan duration;
            using (Stream stream = AssetLoader.Open(_animationUri))
            {
                if (!GifDurationReader.TryRead(stream, out duration))
                {
                    // A pass whose length cannot be measured cannot be stopped after one play, so the
                    // control is not worth showing.
                    return null;
                }
            }

            return new SplashMedia(
                SplashMediaKind.Animation,
                duration,
#pragma warning disable CA2000 // The source is not a local: SplashMedia takes it over and disposes it.
                GifStreamSource.FromUri(_animationUri),
#pragma warning restore CA2000
                null);
        }
#pragma warning disable CA1031 // The GIF decoding failures in Avalonia.Labs.Gif are internal types, so Exception is the only thing to catch.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private static SplashMedia? TryLoadPoster()
    {
        if (!AssetLoader.Exists(_posterUri))
        {
            return null;
        }

        try
        {
            using Stream stream = AssetLoader.Open(_posterUri);
            return new SplashMedia(
                SplashMediaKind.Poster,
                TimeSpan.Zero,
                null,
#pragma warning disable CA2000 // The bitmap is not a local: SplashMedia takes it over and disposes it.
                new Bitmap(stream));
#pragma warning restore CA2000
        }
#pragma warning disable CA1031 // A damaged poster is the same missing-asset case as a damaged animation.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
