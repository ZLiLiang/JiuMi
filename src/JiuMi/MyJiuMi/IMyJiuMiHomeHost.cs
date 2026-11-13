using Avalonia;
using JiuMi.Application.MyJiuMi;

namespace JiuMi.MyJiuMi;

/// <summary>
/// A page host that can report where the cat's bottom-centre currently sits on screen, so the session
/// can hand the cat to the desktop at the same anchor.
/// </summary>
public interface IMyJiuMiHomeHost
{
    /// <summary>
    /// Gets a value indicating whether the page is currently visible and not minimized.
    /// </summary>
    bool IsHomeVisible
    {
        get;
    }

    /// <summary>
    /// Gets the cat's bottom-centre in physical screen pixels, or <see langword="null"/> when the page
    /// has not been laid out yet.
    /// </summary>
    /// <returns>The anchor used by the handoff, or <see langword="null"/>.</returns>
    PixelPoint? GetCatScreenAnchor();

    /// <summary>
    /// Gets the room's visible rectangle in physical screen pixels, or <see langword="null"/> when the
    /// page is not laid out or nothing of the room is on screen.
    /// </summary>
    /// <returns>The visible room, clipped by the page viewport and the window.</returns>
    MyJiuMiScreenRect? GetRoomScreenRect();
}
