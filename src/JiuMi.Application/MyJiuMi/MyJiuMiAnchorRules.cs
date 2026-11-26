namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Normalizes character pack canvas anchors to display-slot DIP offsets for the page and desktop hosts.
/// </summary>
public static class MyJiuMiAnchorRules
{
    /// <summary>
    /// Converts a canvas pixel anchor into normalized <c>[0, 1]</c> slot coordinates.
    /// </summary>
    /// <param name="anchor">The canvas anchor in pixels.</param>
    /// <param name="canvas">The canvas dimensions in pixels.</param>
    /// <returns>
    /// The normalized horizontal and vertical anchor coordinates. A vertical anchor on the bottommost
    /// pixel row (<c>canvas.Height - 1</c>) maps to <c>1.0</c> so legacy bottom-aligned assets have
    /// zero vertical offset.
    /// </returns>
    public static (double NormalizedX, double NormalizedY) Normalize(
        MyJiuMiPetPackAnchor anchor,
        MyJiuMiPetPackCanvas canvas)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0)
        {
            return (0.5, 1.0);
        }

        double normalizedX = Math.Clamp(anchor.X / canvas.Width, 0, 1);
        double normalizedY = anchor.Y >= canvas.Height - 1
            ? 1.0
            : Math.Clamp(anchor.Y / canvas.Height, 0, 1);
        return (normalizedX, normalizedY);
    }

    /// <summary>
    /// Computes the DIP translation applied to a cat frame inside a square display slot so that its
    /// <paramref name="anchor"/> aligns with the slot's bottom-centre floor point.
    /// </summary>
    /// <param name="anchor">The frame's effective canvas anchor.</param>
    /// <param name="canvas">The canvas dimensions in pixels.</param>
    /// <param name="slotSizeDip">The square slot side length in DIP.</param>
    /// <returns>The horizontal and vertical translation in DIP.</returns>
    public static (double OffsetX, double OffsetY) ComputePageFrameOffsetDip(
        MyJiuMiPetPackAnchor anchor,
        MyJiuMiPetPackCanvas canvas,
        double slotSizeDip)
    {
        if (!(slotSizeDip > 0))
        {
            return (0, 0);
        }

        (double normalizedX, double normalizedY) = Normalize(anchor, canvas);
        double offsetX = (0.5 - normalizedX) * slotSizeDip;
        double offsetY = (1.0 - normalizedY) * slotSizeDip;
        return (offsetX, offsetY);
    }

    /// <summary>
    /// Computes the per-frame DIP translation inside the desktop host relative to the pack's
    /// <paramref name="defaultAnchor"/> so per-frame anchor overrides stay steady without moving the
    /// OS window on every frame.
    /// </summary>
    /// <param name="frameAnchor">The current frame's effective anchor.</param>
    /// <param name="defaultAnchor">The pack's default anchor used to position the desktop window.</param>
    /// <param name="canvas">The canvas dimensions in pixels.</param>
    /// <param name="slotSizeDip">The current desktop host size in DIP.</param>
    /// <returns>The horizontal and vertical translation in DIP.</returns>
    public static (double OffsetX, double OffsetY) ComputeDesktopFrameDeltaDip(
        MyJiuMiPetPackAnchor frameAnchor,
        MyJiuMiPetPackAnchor defaultAnchor,
        MyJiuMiPetPackCanvas canvas,
        double slotSizeDip)
    {
        if (!(slotSizeDip > 0))
        {
            return (0, 0);
        }

        (double defaultX, double defaultY) = Normalize(defaultAnchor, canvas);
        (double frameX, double frameY) = Normalize(frameAnchor, canvas);
        return (
            (defaultX - frameX) * slotSizeDip,
            (defaultY - frameY) * slotSizeDip);
    }
}
