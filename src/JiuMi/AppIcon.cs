using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace JiuMi;

/// <summary>
/// Loads the one brand mark the shell shows in its chrome, taskbar entry and, once the tray ships,
/// the tray.
/// </summary>
internal static class AppIcon
{
    private static readonly Lazy<Bitmap?> _mark = new(LoadMark);

    /// <summary>The packaged PNG every runtime icon reads.</summary>
    public static Uri AssetUri { get; } = new("avares://JiuMi/Assets/app-icon.png");

    /// <summary>
    /// Gets the decoded mark shared for the lifetime of the process.
    /// </summary>
    /// <remarks>A missing or damaged asset yields <see langword="null"/> instead of failing startup.</remarks>
    public static Bitmap? Mark => _mark.Value;

    /// <summary>
    /// Wraps the mark for a plain <see cref="Window"/>.
    /// </summary>
    /// <returns>The window icon, or <see langword="null"/> when the mark is unavailable.</returns>
    public static WindowIcon? CreateWindowIcon() => Mark is { } mark ? new WindowIcon(mark) : null;

    private static Bitmap? LoadMark()
    {
        if (!AssetLoader.Exists(AssetUri))
        {
            return null;
        }

        try
        {
            using Stream stream = AssetLoader.Open(AssetUri);
            return new Bitmap(stream);
        }
#pragma warning disable CA1031 // A missing or damaged brand mark falls back to no icon; it must not stop startup.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
